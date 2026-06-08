namespace VoiceRay.Infrastructure

open System
open System.IO
open System.Net.Http

/// Downloads the wav2vec2 espeak phoneme model (ONNX) + vocab when missing.
/// Source: onnx-community/wav2vec2-lv-60-espeak-cv-ft-ONNX (pinned revision).
module Wav2Vec2Provisioner =
    let private provisionLock = obj ()
    let private http = lazy (new HttpClient(Timeout = TimeSpan.FromMinutes 30.0))

    let private repo = "onnx-community/wav2vec2-lv-60-espeak-cv-ft-ONNX"
    // Pinned commit for reproducible downloads (has both model + vocab; see docs/providers.md).
    let private revision = "c69750f5043e5e1f8a71ab95dd3b98338c280c92"
    // int8 dynamic-quantized model: ~318 MB, CPU-friendly for ONNX Runtime.
    let private modelSource = "onnx/model_quantized.onnx"

    let modelDir (repoRoot: string) =
        match Environment.GetEnvironmentVariable "VOICERAY_WAV2VEC2_DIR" with
        | null
        | "" -> Path.Combine(repoRoot, "models", "wav2vec2")
        | dir -> dir

    let modelPath (repoRoot: string) =
        Path.Combine(modelDir repoRoot, "model_quantized.onnx")

    let vocabPath (repoRoot: string) =
        Path.Combine(modelDir repoRoot, "vocab.json")

    let isReady (repoRoot: string) =
        File.Exists(modelPath repoRoot) && File.Exists(vocabPath repoRoot)

    let private resolveUrl (file: string) =
        $"https://huggingface.co/{repo}/resolve/{revision}/{file}"

    let private downloadFile (label: string) (url: string) (destination: string) =
        ProvisionLog.info $"{label}…"

        task {
            Directory.CreateDirectory(Path.GetDirectoryName destination) |> ignore
            use! response = http.Value.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
            response.EnsureSuccessStatusCode() |> ignore
            use! stream = response.Content.ReadAsStreamAsync()
            // Download to a temp file then move, so a partial download never looks "ready".
            let tmp = destination + ".part"
            use file = File.Create tmp
            do! stream.CopyToAsync(file)
            file.Close()

            if File.Exists destination then
                File.Delete destination

            File.Move(tmp, destination)
        }
        |> Async.AwaitTask
        |> Async.RunSynchronously

        ProvisionLog.info $"{label} complete."

    /// Idempotent download of the model + vocab under `models/wav2vec2/`.
    let tryProvision (repoRoot: string) =
        if isReady repoRoot then
            Ok()
        else
            lock provisionLock (fun () ->
                if isReady repoRoot then
                    Ok()
                else
                    try
                        Directory.CreateDirectory(modelDir repoRoot) |> ignore

                        if not (File.Exists(vocabPath repoRoot)) then
                            downloadFile "Downloading wav2vec2 vocab" (resolveUrl "vocab.json") (vocabPath repoRoot)

                        if not (File.Exists(modelPath repoRoot)) then
                            downloadFile
                                "Downloading wav2vec2 phoneme model (~318 MB, one-time)"
                                (resolveUrl modelSource)
                                (modelPath repoRoot)

                        if isReady repoRoot then
                            Ok()
                        else
                            Error "wav2vec2 files are still missing after provisioning."
                    with ex ->
                        Error ex.Message)

    let statusMessage (repoRoot: string) =
        if isReady repoRoot then "ready" else "missing"
