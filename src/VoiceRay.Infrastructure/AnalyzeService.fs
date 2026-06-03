namespace VoiceRay.Infrastructure

open System
open VoiceRay.Core

type AnalyzeServiceError =
    | InvalidRequest of message: string
    | MissingAudio
    | InvalidAudio of message: string
    | G2pUnavailable

type AnalyzeService(alignmentOptions: AlignmentOptions) =
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
                match G2pStub.tryLookup locale text with
                | None -> Error G2pUnavailable
                | Some g2p ->
                    let durationMs =
                        WavDuration.tryGetDurationMs normalized
                        |> Option.defaultValue 320

                    let alignment = OssAlignment.align alignmentOptions locale g2p durationMs
                    let keyframes = PoseMap.keyframesForTimeline locale alignment.Phonemes
                    let scores = AnalyzeScoring.scorePhonemes alignment.Phonemes

                    Ok
                        { Phonemes = alignment.Phonemes
                          Keyframes = keyframes
                          Scores = scores
                          AudioEcho = Some(Convert.ToBase64String normalized)
                          Metadata = OssAlignment.toMetadata alignment }
