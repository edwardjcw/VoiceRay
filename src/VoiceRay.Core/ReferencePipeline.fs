namespace VoiceRay.Core

/// Pure reference session assembly (G2P timeline + articulatory keyframes).
module ReferencePipeline =
    type ReferenceSession =
        { Phonemes: PhonemeSegment list
          Keyframes: ArticulatoryKeyframe list
          IpaDisplay: string }

    let buildSession (locale: Locale) (g2p: G2pStub.G2pResult) (durationMs: int) =
        let durationMs = max 1 durationMs
        let phonemes = G2pStub.buildTimeline g2p.IpaSymbols durationMs
        let keyframes = PoseMap.keyframesForTimeline locale phonemes

        { Phonemes = phonemes
          Keyframes = keyframes
          IpaDisplay = g2p.IpaDisplay }
