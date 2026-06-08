namespace VoiceRay.Infrastructure

open System
open System.IO

/// Ensures Whisper cache directory exists (alignment prefers whisper-stub when present).
module WhisperProvisioner =
    let private defaultCacheDir () =
        let userProfile = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
        Path.Combine(userProfile, ".cache", "whisper")

    let resolveCacheDir (alignment: AlignmentOptions) =
        match alignment.WhisperCacheDir with
        | Some dir when not (String.IsNullOrWhiteSpace dir) -> dir
        | _ -> defaultCacheDir ()

    let isReady (alignment: AlignmentOptions) =
        AlignmentOptions.whisperCacheAvailable alignment

    let private tryCopyFromCloneMyVoice (targetDir: string) =
        let candidates =
            [ Environment.GetEnvironmentVariable "VOICERAY_WHISPER_SOURCE"
              Path.Combine(
                  Environment.GetFolderPath Environment.SpecialFolder.UserProfile,
                  "Source",
                  "Repos",
                  "edwardjcw",
                  "CloneMyVoice",
                  "CloneMyVoice",
                  "models"
              )
              Path.Combine("C:", "Users", Environment.UserName, "Source", "Repos", "edwardjcw", "CloneMyVoice", "CloneMyVoice", "models") ]

        candidates
        |> List.choose (fun p ->
            if String.IsNullOrWhiteSpace p then
                None
            elif Directory.Exists p then
                Some p
            else
                None)
        |> List.tryHead
        |> function
            | None -> false
            | Some source ->
                try
                    ProvisionLog.info $"Copying Whisper models from {source}…"

                    for file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories) do
                        let relative = Path.GetRelativePath(source, file)
                        let dest = Path.Combine(targetDir, relative)
                        Directory.CreateDirectory(Path.GetDirectoryName dest) |> ignore

                        if not (File.Exists dest) then
                            File.Copy(file, dest, true)

                    true
                with ex ->
                    ProvisionLog.warn $"Could not copy Whisper models: {ex.Message}"
                    false

    /// Creates cache directory; copies from CloneMyVoice when available.
    let tryProvision (alignment: AlignmentOptions) =
        if isReady alignment then
            Ok()
        else
            try
                let cacheDir = resolveCacheDir alignment
                Directory.CreateDirectory cacheDir |> ignore
                ProvisionLog.info $"Whisper cache folder: {cacheDir}"

                if Directory.EnumerateFileSystemEntries cacheDir |> Seq.isEmpty then
                    tryCopyFromCloneMyVoice cacheDir |> ignore

                if Directory.EnumerateFileSystemEntries cacheDir |> Seq.isEmpty then
                    let marker = Path.Combine(cacheDir, ".voiceray-whisper-stub")
                    File.WriteAllText(marker, "VoiceRay stub cache — full Whisper models optional for MVP.")
                    ProvisionLog.info "Whisper stub cache marker created (alignment uses whisper-stub)."

                if isReady alignment then
                    Ok()
                else
                    Error "Whisper cache directory could not be prepared."
            with ex ->
                Error ex.Message
