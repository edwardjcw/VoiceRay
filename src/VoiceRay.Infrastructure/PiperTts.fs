namespace VoiceRay.Infrastructure

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Threading.Tasks

type PiperTtsError =
    | NotConfigured
    | ProcessFailed of exitCode: int * stderr: string
    | TimedOut

module PiperTts =
    let private synthesisTimeoutMs = 120_000

    /// Synthesizes `text` with a specific voice model (used for non-default locales).
    let synthesizeWithVoice (options: PiperOptions) (voiceModel: string) (text: string) =
        if not (File.Exists options.Executable && File.Exists voiceModel) then
            Error NotConfigured
        elif String.IsNullOrWhiteSpace text then
            Error(ProcessFailed(-1, "Synthesis text is empty."))
        else
            let tempDir = Path.Combine(Path.GetTempPath(), "voiceray-piper")
            Directory.CreateDirectory tempDir |> ignore

            let outputPath =
                Path.Combine(tempDir, $"ref-{Guid.NewGuid():N}.wav")

            try
                ProvisionLog.info $"Synthesizing \"{text}\" with Piper TTS…"

                let psi =
                    ProcessStartInfo(
                        FileName = options.Executable,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    )

                psi.ArgumentList.Add("-m")
                psi.ArgumentList.Add(voiceModel)
                psi.ArgumentList.Add("-f")
                psi.ArgumentList.Add(outputPath)
                psi.ArgumentList.Add("-q")

                use piperProc = Process.Start psi

                if isNull piperProc then
                    Error(ProcessFailed(-1, "Failed to start Piper process"))
                else
                    let stderrTask =
                        Task.Run(fun () -> piperProc.StandardError.ReadToEnd())

                    piperProc.StandardInput.Write(text.Trim())
                    piperProc.StandardInput.WriteLine()
                    piperProc.StandardInput.Close()

                    if not (piperProc.WaitForExit synthesisTimeoutMs) then
                        try
                            piperProc.Kill true
                        with _ ->
                            ()

                        ProvisionLog.error "Piper TTS timed out."
                        Error TimedOut
                    else
                        let stderr = stderrTask.Result

                        if piperProc.ExitCode <> 0 || not (File.Exists outputPath) then
                            ProvisionLog.error $"Piper failed (exit {piperProc.ExitCode})."
                            Error(ProcessFailed(piperProc.ExitCode, stderr))
                        else
                            let bytes = File.ReadAllBytes outputPath
                            ProvisionLog.info $"Piper synthesis complete ({bytes.Length} bytes)."
                            Ok bytes
            finally
                if File.Exists outputPath then
                    File.Delete outputPath |> ignore

    /// Synthesizes `text` with the default (en-US) voice.
    let synthesize (options: PiperOptions) (text: string) =
        synthesizeWithVoice options options.VoiceModel text
