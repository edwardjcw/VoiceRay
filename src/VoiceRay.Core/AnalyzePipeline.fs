namespace VoiceRay.Core

/// Pure analyze session assembly (aligned phonemes, keyframes, scores).
module AnalyzePipeline =
    type AnalyzeSession =
        { Phonemes: PhonemeSegment list
          Keyframes: ArticulatoryKeyframe list
          Scores: PhonemeScore list }

    let buildSession (locale: Locale) (g2p: G2pStub.G2pResult) (durationMs: int) =
        let durationMs = max 1 durationMs
        let phonemes = G2pStub.buildTimeline g2p.IpaSymbols durationMs
        let keyframes = PoseMap.keyframesForTimeline locale phonemes
        let scores = AnalyzeScoring.scorePhonemes phonemes

        { Phonemes = phonemes
          Keyframes = keyframes
          Scores = scores }
