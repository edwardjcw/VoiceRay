namespace VoiceRay.Infrastructure

open System.IO

/// Resolves the VoiceRay repository root from the API project content root.
module RepoPaths =
    let resolveRepoRoot (contentRoot: string) =
        let candidates =
            [ contentRoot
              Path.GetFullPath(Path.Combine(contentRoot, ".."))
              Path.GetFullPath(Path.Combine(contentRoot, "..", "..")) ]

        candidates
        |> List.tryFind (fun root ->
            File.Exists(Path.Combine(root, "scripts", "whisper_worker.py"))
            || Directory.Exists(Path.Combine(root, "client")))
        |> Option.defaultValue contentRoot
