namespace VoiceRay.Infrastructure

open System
open System.IO
open System.IO.Compression
open System.Net.Http
open System.Threading

/// Downloads Piper CLI and per-locale voices when missing (Windows amd64; see
/// `scripts/provision-piper.ps1`). Voices are pulled from rhasspy/piper-voices.
module PiperProvisioner =
    let private provisionLock = obj ()
    let private http = new HttpClient()

    let private releaseTag = "2023.11.14-2"

    let private voicesRoot =
        "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0"

    /// HuggingFace sub-path for each locale's voice directory (relative to `voicesRoot`).
    let private voiceSubPaths =
        Map.ofList
            [ "en-US", "en/en_US/lessac/medium"
              "fr-FR", "fr/fr_FR/siwis/medium" ]

    let isReady (options: PiperOptions) = PiperOptions.isConfigured options

    let isReadyForLocale (options: PiperOptions) (locale: string) =
        PiperOptions.isVoiceReady options locale

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

    /// Downloads the ONNX voice + its `.json` config for `locale` when missing.
    let private ensureVoiceModelFor (options: PiperOptions) (locale: string) =
        let voicePath = PiperOptions.resolveVoice options locale
        let voiceDir = Path.GetDirectoryName voicePath

        if String.IsNullOrEmpty voiceDir then
            failwith "Invalid Piper voice model path."

        Directory.CreateDirectory voiceDir |> ignore

        let onnxName = Path.GetFileName voicePath
        let jsonName = $"{onnxName}.json"

        match voiceSubPaths.TryFind locale with
        | None -> failwith $"No Piper voice is configured for locale '{locale}'."
        | Some subPath ->
            let voiceBase = $"{voicesRoot}/{subPath}"

            if not (File.Exists voicePath) then
                downloadFile $"Downloading {locale} voice {onnxName}" $"{voiceBase}/{onnxName}" voicePath

            let jsonPath = Path.Combine(voiceDir, jsonName)

            if not (File.Exists jsonPath) then
                downloadFile $"Downloading {locale} voice config {jsonName}" $"{voiceBase}/{jsonName}" jsonPath

    /// Idempotent Piper setup (binary + default en-US voice). Returns `Ok` when ready.
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
                        ensureVoiceModelFor options PiperOptions.defaultLocale

                        if isReady options then
                            Ok()
                        else
                            Error "Piper files are still missing after provisioning."
                    with ex ->
                        Error ex.Message)

    /// Idempotent setup of the Piper binary + the voice for a specific locale.
    let tryProvisionLocale (options: PiperOptions) (locale: string) =
        if isReadyForLocale options locale then
            Ok()
        else
            lock provisionLock (fun () ->
                if isReadyForLocale options locale then
                    Ok()
                else
                    try
                        ensurePiperBinary options
                        ensureVoiceModelFor options locale

                        if isReadyForLocale options locale then
                            Ok()
                        else
                            Error $"Piper voice for '{locale}' is still missing after provisioning."
                    with ex ->
                        Error ex.Message)

    let statusMessage (options: PiperOptions) =
        if isReady options then
            "ready"
        elif OperatingSystem.IsWindows() then
            "not_installed"
        else
            "unsupported_platform"
