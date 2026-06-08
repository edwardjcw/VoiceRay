namespace VoiceRay.Infrastructure

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Threading
open System.Threading.Tasks

type WhisperTranscribeResult =
    | Transcribed of text: string
    | Unavailable of reason: string

/// Whisper ASR via a persistent Python worker (model loads once per API process).
module WhisperTranscriber =
    let private readyTimeoutMs = 120_000
    let private requestTimeoutMs = 30_000

    type private Worker =
        { Process: Process
          Stdin: StreamWriter
          Stdout: StreamReader
          Gate: SemaphoreSlim
          Device: string }

    let private workerLock = obj ()
    let mutable private worker: Worker option = None
    let mutable private lastStartError: string option = None

    let private whisperDevice () =
        match Environment.GetEnvironmentVariable "VOICERAY_WHISPER_DEVICE" with
        | null
        | "" -> "auto"
        | value -> value.Trim().ToLowerInvariant()

    let private readLineWithTimeout (reader: StreamReader) (timeoutMs: int) =
        let task = Task.Run(fun () -> reader.ReadLine())

        if task.Wait timeoutMs then
            task.Result |> Option.ofObj
        else
            None

    let private attachStderrDrain (proc: Process) =
        proc.ErrorDataReceived.Add(fun args ->
            if not (isNull args.Data) then
                ProvisionLog.info $"[whisper] {args.Data}")

        proc.BeginErrorReadLine()

    let private startWorker (contentRoot: string) (cacheDir: string) (modelName: string) (launcher: WhisperPython.Launcher) =
        let scriptPath = Path.Combine(contentRoot, "scripts", "whisper_worker.py")

        if not (File.Exists scriptPath) then
            Error "whisper_worker.py not found"
        else
            try
                let psi =
                    ProcessStartInfo(
                        FileName = launcher.FileName,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    )

                psi.StandardErrorEncoding <- System.Text.Encoding.UTF8

                for arg in launcher.PrefixArgs do
                    psi.ArgumentList.Add arg

                psi.ArgumentList.Add scriptPath
                psi.ArgumentList.Add cacheDir
                psi.ArgumentList.Add modelName
                psi.ArgumentList.Add(whisperDevice ())

                let proc = new Process(StartInfo = psi, EnableRaisingEvents = true)
                proc.Start() |> ignore
                attachStderrDrain proc

                let stdout = proc.StandardOutput

                match readLineWithTimeout stdout readyTimeoutMs with
                | None ->
                    try
                        proc.Kill(entireProcessTree = true)
                    with _ ->
                        ()

                    Error "Whisper worker timed out while loading the model"
                | Some readyLine ->
                    try
                        use doc = JsonDocument.Parse readyLine
                        let root = doc.RootElement

                        if root.TryGetProperty("status") |> fst then
                            let device =
                                if root.TryGetProperty("device") |> fst then
                                    root.GetProperty("device").GetString()
                                else
                                    "unknown"

                            ProvisionLog.info $"Whisper worker ready ({modelName} on {device}, {WhisperPython.describe launcher})."

                            Ok
                                { Process = proc
                                  Stdin = proc.StandardInput
                                  Stdout = stdout
                                  Gate = new SemaphoreSlim(1, 1)
                                  Device = device }
                        else
                            let err =
                                if root.TryGetProperty("error") |> fst then
                                    root.GetProperty("error").GetString()
                                else
                                    readyLine

                            Error $"Whisper worker failed to start: {err}"
                    with ex ->
                        Error $"Whisper worker handshake failed: {ex.Message}"
            with ex ->
                Error $"Whisper worker unavailable: {ex.Message}"

    let private ensureWorker (contentRoot: string) (cacheDir: string) =
        lock workerLock (fun () ->
            match worker with
            | Some w when not w.Process.HasExited -> Ok w
            | _ ->
                match WhisperPython.tryResolve () with
                | None ->
                    lastStartError <- Some "No Python with openai-whisper found (set VOICERAY_PYTHON or pip install openai-whisper)"
                    Error lastStartError.Value
                | Some launcher ->
                    match startWorker contentRoot cacheDir "base.en" launcher with
                    | Ok w ->
                        worker <- Some w
                        lastStartError <- None
                        Ok w
                    | Error message ->
                        lastStartError <- Some message
                        ProvisionLog.warn message
                        Error message)

    let isRuntimeAvailable () = WhisperPython.tryResolve () |> Option.isSome

    /// Stops the worker (for tests).
    let resetWorker () =
        lock workerLock (fun () ->
            match worker with
            | None -> ()
            | Some w ->
                try
                    w.Process.Kill(entireProcessTree = true)
                with _ ->
                    ()

                worker <- None)

    let warmUp (contentRoot: string) (alignmentOptions: AlignmentOptions) =
        if WhisperProvisioner.isReady alignmentOptions then
            let cacheDir = WhisperProvisioner.resolveCacheDir alignmentOptions
            ensureWorker contentRoot cacheDir |> ignore

    let tryTranscribe (contentRoot: string) (alignmentOptions: AlignmentOptions) (normalizedWav: byte[]) =
        if not (WhisperProvisioner.isReady alignmentOptions) then
            Unavailable "Whisper model cache is missing"
        elif not (isRuntimeAvailable ()) then
            Unavailable "openai-whisper is not installed for any Python on PATH"
        else
            let cacheDir = WhisperProvisioner.resolveCacheDir alignmentOptions

            match ensureWorker contentRoot cacheDir with
            | Error reason -> Unavailable reason
            | Ok w ->
                let tempDir = Path.Combine(Path.GetTempPath(), "voiceray-whisper")
                Directory.CreateDirectory tempDir |> ignore
                let wavPath = Path.Combine(tempDir, $"user-{Guid.NewGuid():N}.wav")

                try
                    File.WriteAllBytes(wavPath, normalizedWav)

                    w.Gate.Wait() |> ignore

                    try
                        w.Stdin.WriteLine wavPath
                        w.Stdin.Flush()

                        match readLineWithTimeout w.Stdout requestTimeoutMs with
                        | None ->
                            Unavailable "Whisper transcription timed out"
                        | Some json ->
                            try
                                use doc = JsonDocument.Parse json
                                let root = doc.RootElement

                                if root.TryGetProperty("text") |> fst then
                                    let text = root.GetProperty("text").GetString()

                                    if String.IsNullOrWhiteSpace text then
                                        Unavailable "Whisper returned empty text"
                                    else
                                        ProvisionLog.info $"Whisper heard: \"{text}\" (device={w.Device})"
                                        Transcribed text
                                elif root.TryGetProperty("error") |> fst then
                                    let err = root.GetProperty("error").GetString()
                                    Unavailable $"Whisper error: {err}"
                                else
                                    Unavailable "Whisper returned an unexpected response"
                            with ex ->
                                Unavailable $"Whisper JSON parse failed: {ex.Message}"
                    finally
                        w.Gate.Release() |> ignore
                finally
                    try
                        if File.Exists wavPath then
                            File.Delete wavPath
                    with _ ->
                        ()
