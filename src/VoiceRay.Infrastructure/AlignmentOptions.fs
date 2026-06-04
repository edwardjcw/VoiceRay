namespace VoiceRay.Infrastructure

open Microsoft.Extensions.Configuration

/// OSS alignment provider selection (Whisper preferred, MFA fallback stub).
type AlignmentProvider =
    | Whisper
    | Mfa

type AlignmentOptions =
    { Provider: AlignmentProvider
      WhisperCacheDir: string option
      MfaWorkerUrl: string option }

module AlignmentOptions =
    let sectionName = "Speech:Alignment"

    let load (configuration: IConfiguration) =
        let section = configuration.GetSection(sectionName)

        let provider =
            match section.["Provider"] |> Option.ofObj with
            | Some value when value.Equals("Mfa", System.StringComparison.OrdinalIgnoreCase) -> Mfa
            | _ -> Whisper

        let cacheDir =
            section.GetSection("Whisper").["CacheDir"]
            |> Option.ofObj
            |> Option.filter (not << System.String.IsNullOrWhiteSpace)

        let mfaUrl =
            section.GetSection("Mfa").["WorkerUrl"]
            |> Option.ofObj
            |> Option.filter (not << System.String.IsNullOrWhiteSpace)

        { Provider = provider
          WhisperCacheDir = cacheDir
          MfaWorkerUrl = mfaUrl }

    let whisperCacheAvailable (options: AlignmentOptions) =
        match options.WhisperCacheDir with
        | Some dir -> System.IO.Directory.Exists dir
        | None ->
            let userProfile =
                System.Environment.GetFolderPath System.Environment.SpecialFolder.UserProfile

            System.IO.Directory.Exists(System.IO.Path.Combine(userProfile, ".cache", "whisper"))
