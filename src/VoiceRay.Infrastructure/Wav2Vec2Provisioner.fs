namespace VoiceRay.Infrastructure

open System
open System.IO
open System.Net.Http

/// Downloads the wav2vec2 espeak phoneme model (ONNX) + vocab when missing.
/// Source: onnx-community/wav2vec2-lv-60-espeak-cv-ft-ONNX (pinned revision).
///
/// The model **variant** (precision) is configurable. `model` (full fp32) gives the
/// finest vowel discrimination at the cost of size/RAM; `model_fp16` and
/// `model_quantized` (int8) trade accuracy for a smaller, lighter footprint.
module Wav2Vec2Provisioner =
    let private provisionLock = obj ()
    let private http = lazy (new HttpClient(Timeout = TimeSpan.FromMinutes 60.0))

    let private repo = "onnx-community/wav2vec2-lv-60-espeak-cv-ft-ONNX"
    // Pinned commit for reproducible downloads (has every variant + vocab; see docs/providers.md).
    let private revision = "c69750f5043e5e1f8a71ab95dd3b98338c280c92"

    /// Built-in default when neither env nor config selects a variant.
    /// `model` = full fp32 (~1.2 GB) — best front-vowel discrimination on CPU.
    [<Literal>]
    let defaultVariant = "model"

    let mutable private configuredVariant: string option = None

    /// Canonicalizes a requested variant onto an ONNX filename stem. Accepts a few
    /// friendly aliases; unknown values pass through so new exports work without code edits.
    let canonicalVariant (raw: string) =
        match (raw |> Option.ofObj |> Option.defaultValue "").Trim().ToLowerInvariant() with
        | "" -> defaultVariant
        | "model"
        | "fp32"
        | "full" -> "model"
        | "model_fp16"
        | "fp16" -> "model_fp16"
        | "model_quantized"
        | "int8"
        | "quantized" -> "model_quantized"
        | other -> other

    /// Approximate on-disk size (MB) per known variant, for download messaging.
    let private approxSizeMb (variant: string) =
        match variant with
        | "model" -> 1206
        | "model_fp16" -> 603
        | "model_quantized" -> 303
        | _ -> 0

    /// Records the config-selected variant (env override still wins). Call once at startup.
    let setDefaultVariant (variant: string) =
        configuredVariant <- Some(canonicalVariant variant)

    /// Resolves the active variant: env override > configured default > built-in default.
    let variant () =
        match Environment.GetEnvironmentVariable "VOICERAY_WAV2VEC2_VARIANT" with
        | null
        | "" ->
            match configuredVariant with
            | Some v -> v
            | None -> defaultVariant
        | env -> canonicalVariant env

    let modelDir (repoRoot: string) =
        match Environment.GetEnvironmentVariable "VOICERAY_WAV2VEC2_DIR" with
        | null
        | "" -> Path.Combine(repoRoot, "models", "wav2vec2")
        | dir -> dir

    let modelFileName () = $"{variant ()}.onnx"

    let modelPath (repoRoot: string) =
        Path.Combine(modelDir repoRoot, modelFileName ())

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

    /// Idempotent download of the model (selected variant) + vocab under `models/wav2vec2/`.
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
                            let v = variant ()
                            let sizeMb = approxSizeMb v

                            let sizeHint =
                                if sizeMb > 0 then $" (~{sizeMb} MB, one-time)" else " (one-time)"

                            downloadFile
                                $"Downloading wav2vec2 phoneme model '{v}'{sizeHint}"
                                (resolveUrl $"onnx/{v}.onnx")
                                (modelPath repoRoot)

                        if isReady repoRoot then
                            Ok()
                        else
                            Error "wav2vec2 files are still missing after provisioning."
                    with ex ->
                        Error ex.Message)

    let statusMessage (repoRoot: string) =
        if isReady repoRoot then "ready" else "missing"
