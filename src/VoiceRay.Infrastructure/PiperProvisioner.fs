namespace VoiceRay.Infrastructure

open System
open System.IO
open System.IO.Compression
open System.Net.Http
open System.Threading

/// Downloads Piper CLI and en-US voice when missing (Windows amd64; see `scripts/provision-piper.ps1`).
module PiperProvisioner =
    let private provisionLock = obj ()
    let private http = new HttpClient()

    let private releaseTag = "2023.11.14-2"

    let private voiceBase =
        "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_US/lessac/medium"

    let isReady (options: PiperOptions) = PiperOptions.isConfigured options

    let private downloadFile (label: string) (url: string) (destination: string) =
        ProvisionLog.info $"{label}…"
        task {
            Directory.CreateDirectory(Path.GetDirectoryName destination) |> ignore
            use! stream = http.GetStreamAsync(url)
            use file = File.Create destination
            do! stream.CopyToAsync(file)
        }
        |> Async.AwaitTask
        |> Async.RunSynchronously

        ProvisionLog.info $"{label} complete."

    let private ensurePiperBinary (options: PiperOptions) =
        if File.Exists options.Executable then
            ()
        else
            if not (OperatingSystem.IsWindows()) then
                failwith "Automatic Piper provisioning is supported on Windows only."

            let binDir = Path.GetDirectoryName(Path.GetDirectoryName options.Executable)

            if String.IsNullOrEmpty binDir then
                failwith "Invalid Piper executable path."

            Directory.CreateDirectory binDir |> ignore

            let zipUrl =
                $"https://github.com/rhasspy/piper/releases/download/{releaseTag}/piper_windows_amd64.zip"

            let zipPath = Path.Combine(Path.GetTempPath(), "voiceray-piper_windows_amd64.zip")

            downloadFile "Downloading Piper CLI (~25 MB)" zipUrl zipPath
            ProvisionLog.info "Extracting Piper CLI…"
            ZipFile.ExtractToDirectory(zipPath, binDir, true)
            File.Delete zipPath |> ignore

            if not (File.Exists options.Executable) then
                failwith "Piper executable missing after download."

    let private ensureVoiceModel (options: PiperOptions) =
        let voiceDir = Path.GetDirectoryName options.VoiceModel

        if String.IsNullOrEmpty voiceDir then
            failwith "Invalid Piper voice model path."

        Directory.CreateDirectory voiceDir |> ignore

        let onnxName = Path.GetFileName options.VoiceModel
        let jsonName = $"{onnxName}.json"

        if not (File.Exists options.VoiceModel) then
            downloadFile $"Downloading voice model {onnxName}" $"{voiceBase}/{onnxName}" options.VoiceModel

        let jsonPath = Path.Combine(voiceDir, jsonName)

        if not (File.Exists jsonPath) then
            downloadFile $"Downloading voice config {jsonName}" $"{voiceBase}/{jsonName}" jsonPath

    /// Idempotent Piper setup under configured paths. Returns `Ok` when ready.
    let tryProvision (options: PiperOptions) =
        if isReady options then
            Ok()
        else
            lock provisionLock (fun () ->
                if isReady options then
                    Ok()
                else
                    try
                        ensurePiperBinary options
                        ensureVoiceModel options

                        if isReady options then
                            Ok()
                        else
                            Error "Piper files are still missing after provisioning."
                    with ex ->
                        Error ex.Message)

    let statusMessage (options: PiperOptions) =
        if isReady options then
            "ready"
        elif OperatingSystem.IsWindows() then
            "not_installed"
        else
            "unsupported_platform"
