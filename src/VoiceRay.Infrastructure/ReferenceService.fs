namespace VoiceRay.Infrastructure

open System
open System.IO
open System.Security.Cryptography
open System.Text
open VoiceRay.Core

type ReferenceServiceError =
    | InvalidRequest of message: string
    | G2pUnavailable
    | TtsUnavailable
    | RecognitionUnavailable of message: string

module ReferenceMedia =
    let save (options: PiperOptions) (text: string) (wavBytes: byte[]) =
        try
            Directory.CreateDirectory options.MediaRoot |> ignore
            let hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToLowerInvariant()))
            let id = Convert.ToHexString(hash).ToLowerInvariant()[..15]
            let fileName = $"{id}.wav"
            let fullPath = Path.Combine(options.MediaRoot, fileName)
            File.WriteAllBytes(fullPath, wavBytes)
            Some($"/media/reference/{fileName}")
        with _ ->
            None

/// Reference TTS + articulatory timeline. When the wav2vec2 model is provisioned and the
/// alignment provider is `Wav2Vec2`, the known G2P IPA sequence is CTC forced-aligned against
/// the synthesized Piper audio for real per-phoneme timing; otherwise it falls back to the
/// even-spread `ReferencePipeline.buildSession`.
type ReferenceService(options: PiperOptions, alignmentOptions: AlignmentOptions, repoRoot: string) =
    member _.Options = options

    member private _.BuildSession (locale: Locale) (g2p: G2pStub.G2pResult) (wavBytes: byte[]) (durationMs: int) =
        let fallback () = ReferencePipeline.buildSession locale g2p durationMs

        match alignmentOptions.Provider with
        | AlignmentProvider.Wav2Vec2 when Wav2Vec2Phoneme.isReady repoRoot ->
            match Wav2Vec2Phoneme.tryForcedAlign repoRoot wavBytes g2p.IpaSymbols with
            | Ok aligned when not (List.isEmpty aligned) ->
                { ReferencePipeline.Phonemes = aligned
                  ReferencePipeline.Keyframes = PoseMap.keyframesForTimeline locale aligned
                  ReferencePipeline.IpaDisplay = g2p.IpaDisplay }
            | _ -> fallback () // unmapped symbol / infeasible alignment / model error → even-spread
        | _ -> fallback ()

    /// For arbitrary typed words (no demo-lexicon entry), recognize the phonemes directly
    /// from the synthesized Piper audio with the wav2vec2 espeak model. This works for any
    /// language Piper can synthesize without needing a per-locale pronunciation dictionary.
    member private _.RecognizeSession (locale: Locale) (wavBytes: byte[]) : Result<ReferencePipeline.ReferenceSession, string> =
        if not (Wav2Vec2Phoneme.isReady repoRoot) then
            Error "phoneme recognition model is not provisioned"
        else
            match AudioNormalizer.normalize wavBytes with
            | Error _ -> Error "could not normalize synthesized audio for recognition"
            | Ok normalized ->
                match Wav2Vec2Phoneme.tryRecognize repoRoot normalized with
                | Wav2Vec2Result.Recognized(phonemes, ipa) ->
                    Ok
                        { ReferencePipeline.Phonemes = phonemes
                          ReferencePipeline.Keyframes = PoseMap.keyframesForTimeline locale phonemes
                          ReferencePipeline.IpaDisplay = ipa }
                | Wav2Vec2Result.Unavailable reason -> Error reason

    member this.Generate(request: ReferenceRequest) =
        if String.IsNullOrWhiteSpace request.Text then
            Error(InvalidRequest "text is required")
        elif String.IsNullOrWhiteSpace request.Locale then
            Error(InvalidRequest "locale is required")
        else
            let voiceModel = PiperOptions.resolveVoice options request.Locale

            // Ensure the binary + the voice for this locale are present (download French
            // voice on demand the first time it's requested).
            let ttsReady =
                if PiperOptions.isVoiceReady options request.Locale then
                    true
                elif OperatingSystem.IsWindows() then
                    match PiperProvisioner.tryProvisionLocale options request.Locale with
                    | Ok () -> true
                    | Error _ -> false
                else
                    false

            if not ttsReady then
                Error TtsUnavailable
            else
                let wavResult : Result<byte[], ReferenceServiceError> =
                    match PiperTts.synthesizeWithVoice options voiceModel request.Text with
                    | Ok wav -> Ok wav
                    | Error PiperTtsError.NotConfigured -> Error TtsUnavailable
                    | Error PiperTtsError.TimedOut -> Error TtsUnavailable
                    | Error(PiperTtsError.ProcessFailed _) -> Error TtsUnavailable

                match wavResult with
                | Error _ -> Error TtsUnavailable
                | Ok wavBytes ->
                    let durationMs =
                        WavDuration.tryGetDurationMs wavBytes
                        |> Option.defaultValue 320

                    // Known demo word → exact G2P (forced-aligned); otherwise recognize
                    // the phonemes acoustically from the synthesized audio.
                    let sessionResult : Result<ReferencePipeline.ReferenceSession, ReferenceServiceError> =
                        match G2pStub.tryLookup request.Locale request.Text with
                        | Some g2p -> Ok(this.BuildSession request.Locale g2p wavBytes durationMs)
                        | None ->
                            match this.RecognizeSession request.Locale wavBytes with
                            | Ok session -> Ok session
                            | Error reason -> Error(RecognitionUnavailable reason)

                    match sessionResult with
                    | Error err -> Error err
                    | Ok session ->
                        let audioUrl = ReferenceMedia.save options request.Text wavBytes

                        let audioBase64 =
                            if audioUrl.IsSome then
                                None
                            else
                                Some(Convert.ToBase64String wavBytes)

                        Ok
                            { AudioUrl = audioUrl
                              AudioBase64 = audioBase64
                              Phonemes = session.Phonemes
                              Keyframes = session.Keyframes
                              IpaDisplay = session.IpaDisplay }
