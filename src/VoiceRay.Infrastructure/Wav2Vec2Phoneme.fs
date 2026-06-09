namespace VoiceRay.Infrastructure

open System
open System.Collections.Generic
open Microsoft.ML.OnnxRuntime
open VoiceRay.Core

/// Result of running the wav2vec2 phoneme model on user audio.
type Wav2Vec2Result =
    /// Recognized phoneme segments (CTC-timed) plus the concatenated IPA string.
    | Recognized of phonemes: PhonemeSegment list * ipa: string
    | Unavailable of reason: string

/// In-process phoneme recognition + alignment using the wav2vec2 espeak ONNX model.
/// Replaces the heuristic alignment / whisper-word / DSP-vowel stack when provisioned.
module Wav2Vec2Phoneme =
    let private sessionLock = obj ()
    let mutable private session: InferenceSession option = None
    let mutable private sessionPath = ""
    let mutable private inputName = "input_values"
    let mutable private outputName = "logits"
    let mutable private vocab: Wav2Vec2Vocab.Vocab option = None
    let mutable private lastLoadError: string option = None

    let isReady (repoRoot: string) = Wav2Vec2Provisioner.isReady repoRoot

    /// Disposes any cached session (tests / re-provisioning).
    let resetSession () =
        lock sessionLock (fun () ->
            match session with
            | Some s ->
                try
                    s.Dispose()
                with _ ->
                    ()
            | None -> ()

            session <- None
            sessionPath <- ""
            vocab <- None)

    let private ensureSession (repoRoot: string) =
        lock sessionLock (fun () ->
            let modelPath = Wav2Vec2Provisioner.modelPath repoRoot

            match session with
            | Some s when sessionPath = modelPath -> Some(s, vocab)
            | _ ->
                try
                    match Wav2Vec2Vocab.tryLoad (Wav2Vec2Provisioner.vocabPath repoRoot) with
                    | None ->
                        ProvisionLog.warn "wav2vec2 vocab.json is missing or invalid."
                        None
                    | Some v ->
                        let opts = new SessionOptions()
                        let s = new InferenceSession(modelPath, opts)
                        inputName <- s.InputNames |> Seq.head
                        outputName <- s.OutputNames |> Seq.head
                        session <- Some s
                        sessionPath <- modelPath
                        vocab <- Some v
                        ProvisionLog.info "wav2vec2 phoneme model loaded."
                        Some(s, Some v)
                with ex ->
                    let detail =
                        match ex.InnerException with
                        | null -> ex.Message
                        | inner -> $"{ex.Message} :: {inner.Message}"

                    lastLoadError <- Some detail
                    ProvisionLog.warn $"wav2vec2 model load failed: {detail}"
                    None)

    /// Loads the model ahead of the first request (best-effort).
    let warmUp (repoRoot: string) =
        if isReady repoRoot then
            ensureSession repoRoot |> ignore

    /// HF Wav2Vec2 feature extractor: float waveform in [-1,1], zero-mean unit-variance.
    let private toInputValues (samples: int16[]) : float32[] =
        let n = samples.Length
        let x = Array.zeroCreate<float32> n
        let mutable sum = 0.0

        for i in 0 .. n - 1 do
            let v = float samples.[i] / 32768.0
            x.[i] <- float32 v
            sum <- sum + v

        let mean = if n > 0 then sum / float n else 0.0
        let mutable varSum = 0.0

        for i in 0 .. n - 1 do
            let d = float x.[i] - mean
            varSum <- varSum + d * d

        let std = sqrt (varSum / float (max 1 n) + 1e-7)

        for i in 0 .. n - 1 do
            x.[i] <- float32 ((float x.[i] - mean) / std)

        x

    let private runLogits (s: InferenceSession) (input: float32[]) : float32[][] =
        let shape = [| 1L; int64 input.Length |]
        use inputValue = OrtValue.CreateTensorValueFromMemory(input, shape)
        let inputs = Dictionary<string, OrtValue>()
        inputs.[inputName] <- inputValue
        use runOptions = new RunOptions()
        use outputs = s.Run(runOptions, inputs, [| outputName |])
        let outVal = outputs.[0]
        let shapeInfo = outVal.GetTensorTypeAndShape()
        let dims = shapeInfo.Shape // [1; T; V]
        let frames = int dims.[1]
        let vocabSize = int dims.[2]
        let flat = outVal.GetTensorDataAsSpan<float32>().ToArray()

        Array.init frames (fun fi ->
            let baseIdx = fi * vocabSize
            Array.init vocabSize (fun vi -> flat.[baseIdx + vi]))

    /// Recognizes the phonemes the user actually produced (greedy CTC + per-phoneme timing).
    let tryRecognize (repoRoot: string) (normalizedWav: byte[]) : Wav2Vec2Result =
        if not (isReady repoRoot) then
            Unavailable "wav2vec2 model is not provisioned"
        else
            match AudioNormalizer.tryParsePcm normalizedWav with
            | None -> Unavailable "could not parse normalized WAV for wav2vec2"
            | Some pcm ->
                match ensureSession repoRoot with
                | None ->
                    match lastLoadError with
                    | Some e -> Unavailable $"wav2vec2 model could not be loaded: {e}"
                    | None -> Unavailable "wav2vec2 model could not be loaded"
                | Some(_, None) -> Unavailable "wav2vec2 vocab unavailable"
                | Some(s, Some v) ->
                    try
                        let input = toInputValues pcm.Samples
                        let logits = runLogits s input
                        let frames = logits.Length

                        let durationMs =
                            if pcm.SampleRate > 0 then
                                pcm.Samples.Length * 1000 / pcm.SampleRate
                            else
                                pcm.Samples.Length * 1000 / 16000

                        let segments =
                            Ctc.greedyDecode v.BlankId logits
                            |> List.choose (fun span ->
                                match Wav2Vec2Vocab.spanToIpa v span with
                                | None -> None
                                | Some ipa ->
                                    let startMs, endMs = Ctc.spanToMs frames durationMs span

                                    Some
                                        { Ipa = ipa
                                          StartMs = startMs
                                          EndMs = endMs })

                        if List.isEmpty segments then
                            Unavailable "wav2vec2 produced no phonemes"
                        else
                            let ipa = segments |> List.map (fun seg -> seg.Ipa) |> String.concat ""
                            Recognized(segments, ipa)
                    with ex ->
                        Unavailable $"wav2vec2 inference failed: {ex.Message}"

    /// Reverse of `Wav2Vec2Vocab.normalizeIpa`: maps an en-US inventory IPA symbol back to a
    /// model token id. Prefers an exact vocab token, else the lowest-id token whose
    /// `normalizeIpa` equals the target symbol. Specials/blanks are never selected.
    let private targetTokenId (v: Wav2Vec2Vocab.Vocab) (symbol: string) : int option =
        match v.TokenToId.TryFind symbol with
        | Some id when not (v.SpecialIds.Contains id) -> Some id
        | _ ->
            v.IdToToken
            |> Map.toSeq
            |> Seq.sortBy fst
            |> Seq.tryPick (fun (id, tok) ->
                if v.SpecialIds.Contains id then None
                elif Wav2Vec2Vocab.normalizeIpa tok = symbol then Some id
                else None)

    /// Forced-aligns a KNOWN target IPA sequence (e.g. reference G2P) to the audio via CTC
    /// Viterbi, producing real per-phoneme timestamps. The original target IPA symbols are kept
    /// on the returned segments (not the model tokens). Returns `Error` (so callers can fall
    /// back) when the model is absent, a symbol cannot be mapped, or alignment is infeasible.
    let tryForcedAlign (repoRoot: string) (normalizedWav: byte[]) (targetIpa: string list) : Result<PhonemeSegment list, string> =
        if not (isReady repoRoot) then
            Error "wav2vec2 model is not provisioned"
        elif List.isEmpty targetIpa then
            Error "no target phonemes to align"
        else
            match AudioNormalizer.normalize normalizedWav with
            | Error _ -> Error "could not normalize WAV for wav2vec2 forced alignment"
            | Ok normalized ->
                match AudioNormalizer.tryParsePcm normalized with
                | None -> Error "could not parse normalized WAV for wav2vec2 forced alignment"
                | Some pcm ->
                    match ensureSession repoRoot with
                    | None ->
                        match lastLoadError with
                        | Some e -> Error $"wav2vec2 model could not be loaded: {e}"
                        | None -> Error "wav2vec2 model could not be loaded"
                    | Some(_, None) -> Error "wav2vec2 vocab unavailable"
                    | Some(s, Some v) ->
                        let mapped = targetIpa |> List.map (fun sym -> sym, targetTokenId v sym)

                        match mapped |> List.tryPick (fun (sym, id) -> if Option.isNone id then Some sym else None) with
                        | Some missing -> Error $"could not map IPA symbol '{missing}' to a wav2vec2 token"
                        | None ->
                            let tokenIds = mapped |> List.map (fun (_, id) -> Option.get id)

                            try
                                let input = toInputValues pcm.Samples
                                let logits = runLogits s input
                                let frames = logits.Length

                                let durationMs =
                                    if pcm.SampleRate > 0 then
                                        pcm.Samples.Length * 1000 / pcm.SampleRate
                                    else
                                        pcm.Samples.Length * 1000 / 16000

                                match Ctc.forcedAlign v.BlankId tokenIds logits with
                                | None -> Error "wav2vec2 forced alignment is infeasible for this audio"
                                | Some spans ->
                                    let segments =
                                        List.zip targetIpa spans
                                        |> List.map (fun (sym, span) ->
                                            let startMs, endMs = Ctc.spanToMs frames durationMs span

                                            { Ipa = sym
                                              StartMs = startMs
                                              EndMs = endMs })

                                    Ok segments
                            with ex ->
                                Error $"wav2vec2 forced alignment failed: {ex.Message}"
