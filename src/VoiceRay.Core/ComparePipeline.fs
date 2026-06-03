namespace VoiceRay.Core

/// Pure compare session: greedy phoneme diff + locale coaching messages.
module ComparePipeline =
    let compare (locale: Locale) (reference: PhonemeSegment list) (user: PhonemeSegment list) : CompareResponse =
        let segments = PhonemeDiff.alignGreedy reference user
        let coaching = CoachingRules.forSegments locale segments

        { Segments = segments
          Coaching = coaching }
