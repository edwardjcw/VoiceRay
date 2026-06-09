namespace VoiceRay.Infrastructure

open Microsoft.Extensions.Configuration

/// OSS alignment provider selection. `Wav2Vec2` (in-process ONNX phoneme model) is
/// preferred when provisioned; `Whisper`/`Mfa` remain as fallbacks.
type AlignmentProvider =
    | Whisper
    | Mfa
    | Wav2Vec2

type AlignmentOptions =
    { Provider: AlignmentProvider
      WhisperCacheDir: string option
      MfaWorkerUrl: string option }

module AlignmentOptions =
    let sectionName = "Speech:Alignment"

    let load (configuration: IConfiguration) (contentRoot: string) =
        let section = configuration.GetSection(sectionName)
        let repoRoot = RepoPaths.resolveRepoRoot contentRoot

        let provider =
            match section.["Provider"] |> Option.ofObj with
            | Some value when value.Equals("Mfa", System.StringComparison.OrdinalIgnoreCase) -> Mfa
            | Some value when
                value.Equals("Wav2Vec2", System.StringComparison.OrdinalIgnoreCase)
                || value.Equals("Phoneme", System.StringComparison.OrdinalIgnoreCase)
                ->
                Wav2Vec2
            | _ -> Whisper

        let cacheDir =
            section.GetSection("Whisper").["CacheDir"]
            |> Option.ofObj
            |> Option.filter (not << System.String.IsNullOrWhiteSpace)
            |> Option.map (fun dir ->
                if System.IO.Path.IsPathRooted dir then
                    dir
                else
                    System.IO.Path.GetFullPath(System.IO.Path.Combine(repoRoot, dir)))

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
