namespace VoiceRay.Infrastructure

open System
open VoiceRay.Core

/// Infers what the user actually spoke from audio (not just the practice word text).
module UserPhonemeInference =
    type InferenceSource =
        | TextG2p
        | AcousticVowel
        | WhisperTranscript of spokenWord: string

    type InferenceResult =
        { Phonemes: PhonemeSegment list
          Source: InferenceSource
          Note: string option }

    let sourceLabel =
        function
        | TextG2p -> "text-g2p"
        | AcousticVowel -> "acoustic-vowel"
        | WhisperTranscript w -> $"whisper:{w}"

    let inferredWord =
        function
        | WhisperTranscript w -> Some w
        | _ -> None

    let private phonemesForWord (spoken: string) (durationMs: int) =
        G2pStub.tryLookup "en-US" spoken
        |> Option.map (fun g2p ->
            { Phonemes = G2pStub.buildTimeline g2p.IpaSymbols durationMs
              Source = WhisperTranscript spoken
              Note = None })

    let private tryWhisperWord
        (contentRoot: string)
        (alignmentOptions: AlignmentOptions)
        (normalizedWav: byte[])
        (practiceWord: string)
        (durationMs: int)
        =
        match WhisperTranscriber.tryTranscribe contentRoot alignmentOptions normalizedWav with
        | WhisperTranscribeResult.Unavailable reason -> Error reason
        | WhisperTranscribeResult.Transcribed transcript ->
            match G2pStub.tryParseTranscriptForPractice practiceWord transcript with
            | None -> Error $"Whisper heard \"{transcript}\" but no demo word matched"
            | Some spoken ->
                if spoken = G2pStub.normalizeWord practiceWord then
                    Error $"Whisper heard \"{spoken}\" (same as practice word)"
                else
                    match phonemesForWord spoken durationMs with
                    | None -> Error $"Whisper heard \"{spoken}\" but G2P is unavailable"
                    | Some result -> Ok result

    /// Whisper ASR first (word-level), then acoustic vowel refinement.
    let infer
        (contentRoot: string)
        (alignmentOptions: AlignmentOptions)
        (piperOptions: PiperOptions)
        (normalizedWav: byte[])
        (locale: Locale)
        (practiceWord: string)
        (baselinePhonemes: PhonemeSegment list)
        (durationMs: int)
        =
        if locale <> "en-US" then
            { Phonemes = baselinePhonemes
              Source = TextG2p
              Note = None }
        else
            match tryWhisperWord contentRoot alignmentOptions normalizedWav practiceWord durationMs with
            | Ok result -> result
            | Error whisperReason ->
                let acousticPhonemes =
                    AcousticVowelProbe.refinePhonemes
                        normalizedWav
                        locale
                        practiceWord
                        baselinePhonemes
                        piperOptions

                if acousticPhonemes <> baselinePhonemes then
                    { Phonemes = acousticPhonemes
                      Source = AcousticVowel
                      Note = Some $"Whisper: {whisperReason}" }
                else
                    { Phonemes = baselinePhonemes
                      Source = TextG2p
                      Note = Some whisperReason }
