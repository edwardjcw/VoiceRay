namespace VoiceRay.Infrastructure

open System
open VoiceRay.Core

type AnalyzeServiceError =
    | InvalidRequest of message: string
    | MissingAudio
    | InvalidAudio of message: string
    | G2pUnavailable

type AnalyzeService(alignmentOptions: AlignmentOptions, piperOptions: PiperOptions, contentRoot: string) =
    member _.AlignmentOptions = alignmentOptions

    member _.Analyze(audioBytes: byte[], text: string, locale: string) =
        if String.IsNullOrWhiteSpace text then
            Error(InvalidRequest "text is required")
        elif String.IsNullOrWhiteSpace locale then
            Error(InvalidRequest "locale is required")
        elif isNull audioBytes || audioBytes.Length = 0 then
            Error MissingAudio
        else
            match AudioNormalizer.normalize audioBytes with
            | Error AudioNormalizer.EmptyInput -> Error MissingAudio
            | Error(AudioNormalizer.InvalidWav message) -> Error(InvalidAudio message)
            | Error(AudioNormalizer.UnsupportedFormat message) -> Error(InvalidAudio message)
            | Ok normalized ->
                // Known demo word → exact G2P baseline; arbitrary/typed word → empty baseline
                // and rely on acoustic phoneme recognition (wav2vec2).
                let g2p =
                    G2pStub.tryLookup locale text
                    |> Option.defaultValue { IpaSymbols = []; IpaDisplay = "" }

                let durationMs =
                    WavDuration.tryGetDurationMs normalized
                    |> Option.defaultValue 320

                let alignment = OssAlignment.align alignmentOptions locale g2p durationMs

                let inference =
                    UserPhonemeInference.infer
                        contentRoot
                        alignmentOptions
                        piperOptions
                        normalized
                        locale
                        text
                        alignment.Phonemes
                        durationMs

                let phonemes = inference.Phonemes

                if List.isEmpty phonemes then
                    // No demo-lexicon entry and recognition unavailable → nothing to show.
                    Error G2pUnavailable
                else
                    let keyframes = PoseMap.keyframesForTimeline locale phonemes
                    let scores = AnalyzeScoring.scorePhonemes phonemes

                    let baseMetadata =
                        match inference.Source with
                        | UserPhonemeInference.Wav2Vec2Recognized _ ->
                            { OssAlignment.toMetadata alignment with AlignmentEngine = "wav2vec2" }
                        | _ -> OssAlignment.toMetadata alignment

                    let metadata =
                        OssAlignment.withInference
                            baseMetadata
                            (Some(UserPhonemeInference.sourceLabel inference.Source))
                            (UserPhonemeInference.inferredWord inference.Source)
                            inference.Note

                    Ok
                        { Phonemes = phonemes
                          Keyframes = keyframes
                          Scores = scores
                          AudioEcho = Some(Convert.ToBase64String normalized)
                          Metadata = metadata }
