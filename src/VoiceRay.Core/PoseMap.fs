namespace VoiceRay.Core

/// IPA → articulatory pose for `en-US`. Emits normalized articulator parameters
/// (height/backness/rounding/tip/closure/velum) grounded in the IPA vowel chart
/// and consonant place/manner. The frontend `SagittalPlayer` renders geometry.
module PoseMap =
    type PoseEntry =
        { Pose: ArticulatoryPose
          Highlight: string list }

    /// Resting articulation (schwa-like). Matches the rig neutral pose.
    let neutral: ArticulatoryPose =
        { JawOpen = 0.35
          TongueHeight = 0.45
          TongueBackness = 0.45
          TongueTip = 0.0
          Interdental = 0.0
          LipRounding = 0.0
          LipClosure = 0.0
          Velum = 0.0 }

    let private pose
        (jawOpen, tongueHeight, tongueBackness, tongueTip, interdental, lipRounding, lipClosure, velum)
        highlight
        =
        { Pose =
            { JawOpen = jawOpen
              TongueHeight = tongueHeight
              TongueBackness = tongueBackness
              TongueTip = tongueTip
              Interdental = interdental
              LipRounding = lipRounding
              LipClosure = lipClosure
              Velum = velum }
          Highlight = highlight }

    // Vowels: height (≈ inverse F1 / jaw), backness (≈ inverse F2), rounding.
    //                         jaw  hgt  bak  tip  intr rnd  cls  vel
    let private vowelAe = pose (0.82, 0.18, 0.20, 0.0, 0.0, 0.0, 0.0, 0.0) [ "open_vowel" ]
    let private vowelEh = pose (0.55, 0.42, 0.25, 0.0, 0.0, 0.0, 0.0, 0.0) [ "mid_front_vowel" ]
    let private vowelIh = pose (0.32, 0.72, 0.28, 0.0, 0.0, 0.0, 0.0, 0.0) [ "high_front_vowel" ]
    let private vowelIy = pose (0.20, 0.95, 0.15, 0.0, 0.0, 0.0, 0.0, 0.0) [ "high_front_vowel" ]
    let private vowelAh = pose (0.90, 0.10, 0.90, 0.0, 0.0, 0.0, 0.0, 0.0) [ "open_back_vowel" ]
    let private vowelUh = pose (0.34, 0.70, 0.80, 0.0, 0.0, 0.65, 0.0, 0.0) [ "rounded_back_vowel" ]
    let private vowelAo = pose (0.60, 0.35, 0.85, 0.0, 0.0, 0.60, 0.0, 0.0) [ "rounded_back_vowel" ]
    let private vowelUw = pose (0.20, 0.90, 0.90, 0.0, 0.0, 0.90, 0.0, 0.0) [ "rounded_back_vowel" ]
    let private vowelSchwa = pose (0.40, 0.50, 0.50, 0.0, 0.0, 0.0, 0.0, 0.0) [ "mid_central_vowel" ]

    // Consonants.
    let private bilabialStop = pose (0.10, 0.40, 0.45, 0.0, 0.0, 0.0, 1.0, 0.0) [ "bilabial" ]
    let private bilabialNasal = pose (0.10, 0.40, 0.45, 0.0, 0.0, 0.0, 1.0, 1.0) [ "bilabial"; "nasal" ]
    let private alveolarStop = pose (0.18, 0.55, 0.20, 1.0, 0.0, 0.0, 0.0, 0.0) [ "tongue_tip"; "alveolar" ]
    let private alveolarNasal = pose (0.18, 0.55, 0.20, 1.0, 0.0, 0.0, 0.0, 1.0) [ "tongue_tip"; "alveolar"; "nasal" ]
    let private lateral = pose (0.22, 0.50, 0.30, 0.80, 0.0, 0.0, 0.0, 0.0) [ "tongue_tip"; "lateral" ]
    let private velarStop = pose (0.20, 0.88, 0.95, 0.0, 0.0, 0.0, 0.0, 0.0) [ "velar" ]
    let private velarNasal = pose (0.20, 0.88, 0.95, 0.0, 0.0, 0.0, 0.0, 1.0) [ "velar"; "nasal" ]
    let private interdental = pose (0.22, 0.50, 0.20, 1.0, 1.0, 0.0, 0.0, 0.0) [ "interdental"; "theta" ]
    let private postAlveolar = pose (0.28, 0.60, 0.40, 0.55, 0.0, 0.30, 0.0, 0.0) [ "post_alveolar" ]
    let private rhotic = pose (0.30, 0.55, 0.55, 0.30, 0.0, 0.20, 0.0, 0.0) [ "rhotic" ]

    // French additions: front rounded vowels, nasal vowels, uvular /ʁ/, palatals.
    //                          jaw  hgt  bak  tip  intr rnd  cls  vel
    let private vowelY = pose (0.22, 0.92, 0.18, 0.0, 0.0, 0.92, 0.0, 0.0) [ "high_front_rounded_vowel" ]
    let private vowelEu = pose (0.40, 0.62, 0.25, 0.0, 0.0, 0.70, 0.0, 0.0) [ "mid_front_rounded_vowel" ]
    let private vowelOe = pose (0.55, 0.42, 0.28, 0.0, 0.0, 0.65, 0.0, 0.0) [ "open_mid_front_rounded_vowel" ]
    let private vowelO = pose (0.40, 0.62, 0.85, 0.0, 0.0, 0.80, 0.0, 0.0) [ "rounded_back_vowel" ]
    let private vowelAFront = pose (0.85, 0.15, 0.25, 0.0, 0.0, 0.0, 0.0, 0.0) [ "open_front_vowel" ]
    let private nasalAh = pose (0.88, 0.12, 0.88, 0.0, 0.0, 0.10, 0.0, 1.0) [ "open_back_vowel"; "nasal" ]
    let private nasalEh = pose (0.55, 0.42, 0.25, 0.0, 0.0, 0.0, 0.0, 1.0) [ "mid_front_vowel"; "nasal" ]
    let private nasalOh = pose (0.45, 0.55, 0.85, 0.0, 0.0, 0.75, 0.0, 1.0) [ "rounded_back_vowel"; "nasal" ]
    let private nasalOe = pose (0.55, 0.42, 0.28, 0.0, 0.0, 0.60, 0.0, 1.0) [ "front_rounded_vowel"; "nasal" ]
    let private uvular = pose (0.35, 0.45, 0.95, 0.0, 0.0, 0.0, 0.0, 0.0) [ "uvular" ]
    let private palatalNasal = pose (0.22, 0.80, 0.45, 0.40, 0.0, 0.0, 0.0, 1.0) [ "palatal"; "nasal" ]
    let private palatalGlide = pose (0.20, 0.90, 0.20, 0.0, 0.0, 0.85, 0.0, 0.0) [ "labial_palatal"; "glide" ]
    let private palatalApprox = pose (0.22, 0.88, 0.18, 0.0, 0.0, 0.0, 0.0, 0.0) [ "palatal"; "glide" ]
    let private labioVelar = pose (0.22, 0.80, 0.90, 0.0, 0.0, 0.90, 0.0, 0.0) [ "labial_velar"; "glide" ]
    let private labiodental = pose (0.15, 0.45, 0.40, 0.0, 0.0, 0.0, 0.0, 0.0) [ "labiodental" ]
    let private alveolarFric = pose (0.18, 0.60, 0.25, 0.55, 0.0, 0.0, 0.0, 0.0) [ "alveolar"; "fricative" ]

    /// Unified IPA → pose inventory (en-US demo set + common French phonemes).
    /// Lookup is locale-agnostic so typed words in any language animate when their
    /// IPA is known; unknown IPA falls back to the neutral pose.
    let private poseInventory =
        Map.ofList
            [ "p", bilabialStop
              "b", bilabialStop
              "m", bilabialNasal
              "t", alveolarStop
              "d", alveolarStop
              "n", alveolarNasal
              "l", lateral
              "k", velarStop
              "ɡ", velarStop
              "g", velarStop
              "ŋ", velarNasal
              "θ", interdental
              "ð", interdental
              "ʃ", postAlveolar
              "ʒ", postAlveolar
              "ɹ", rhotic
              "æ", vowelAe
              "ɛ", vowelEh
              "ɪ", vowelIh
              "i", vowelIy
              "ɑ", vowelAh
              "ʊ", vowelUh
              "ɔ", vowelAo
              "u", vowelUw
              "ə", vowelSchwa
              "ɚ", rhotic
              "ɝ", rhotic
              // French
              "f", labiodental
              "v", labiodental
              "s", alveolarFric
              "z", alveolarFric
              "j", palatalApprox
              "w", labioVelar
              "ɥ", palatalGlide
              "ɲ", palatalNasal
              "ʁ", uvular
              "ʀ", uvular
              "χ", uvular
              "ʔ", velarStop
              "y", vowelY
              "ø", vowelEu
              "œ", vowelOe
              "o", vowelO
              "e", vowelEh
              "a", vowelAFront
              "ɐ", vowelSchwa
              "ɑ̃", nasalAh
              "ɛ̃", nasalEh
              "ɔ̃", nasalOh
              "œ̃", nasalOe ]

    /// Looks up a pose for an IPA symbol. Locale is accepted for API symmetry but the
    /// inventory is shared across languages (IPA is universal).
    let tryGetPose (_locale: Locale) (ipa: string) = poseInventory.TryFind ipa

    /// Always emits a keyframe; unknown IPA falls back to the neutral pose so the
    /// timeline stays visible and the rig returns to rest.
    let keyframeForSegment (locale: Locale) (segment: PhonemeSegment) =
        let entry =
            tryGetPose locale segment.Ipa
            |> Option.defaultValue { Pose = neutral; Highlight = [] }

        { Ipa = segment.Ipa
          StartMs = segment.StartMs
          EndMs = segment.EndMs
          Pose = entry.Pose
          Highlight = entry.Highlight }

    let keyframesForTimeline (locale: Locale) (phonemes: PhonemeSegment list) =
        phonemes |> List.map (keyframeForSegment locale)
