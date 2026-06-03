namespace VoiceRay.Core

/// IPA → sagittal layer poses for `en-US` (aligned with `client/src/animation/SagittalPlayer.js` test poses).
module PoseMap =
    type PoseEntry =
        { Layers: Map<string, LayerPose>
          Highlight: string list }

    let private layer transform d =
        { Transform = transform
          D = d }

    let private bilabialStop =
        { Layers =
            Map.ofList
                [ "lips_upper", layer (Some "translate(0,4) scale(1,0.85)") None
                  "lips_lower", layer (Some "translate(0,-2) scale(1,0.88)") None
                  "jaw", layer (Some "translate(0,2)") None
                  "tongue",
                  layer
                      None
                      (Some
                          "M 48 108 Q 62 100 88 102 Q 118 106 132 118 Q 142 128 138 142 Q 132 156 108 162 Q 82 166 62 158 Q 48 150 44 136 Q 42 122 48 108 Z") ]
          Highlight = [ "bilabial" ] }

    let private alveolarStop =
        { Layers =
            Map.ofList
                [ "lips_upper", layer (Some "translate(0,0)") None
                  "lips_lower", layer (Some "translate(0,2)") None
                  "jaw", layer (Some "translate(0,3)") None
                  "tongue",
                  layer
                      None
                      (Some "M 46 96 Q 58 82 78 78 Q 92 76 98 88 Q 100 96 92 102 Q 78 108 62 106 Q 50 102 46 96 Z") ]
          Highlight = [ "tongue_tip"; "alveolar" ] }

    let private velarStop =
        { Layers =
            Map.ofList
                [ "lips_upper", layer (Some "translate(0,1)") None
                  "lips_lower", layer (Some "translate(0,3)") None
                  "jaw", layer (Some "translate(0,4)") None
                  "tongue",
                  layer
                      None
                      (Some "M 40 100 Q 52 88 72 84 Q 96 82 108 94 Q 114 104 104 112 Q 88 118 68 116 Q 52 112 40 100 Z")
                  "velum", layer (Some "translate(0,0)") None ]
          Highlight = [ "velar" ] }

    let private openVowel =
        { Layers =
            Map.ofList
                [ "lips_upper", layer (Some "translate(0,1)") None
                  "lips_lower", layer (Some "translate(0,6) scale(1,1.08)") None
                  "jaw", layer (Some "translate(0,10) rotate(6 80 160)") None
                  "teeth_lower", layer (Some "translate(0,8)") None
                  "tongue",
                  layer
                      None
                      (Some
                          "M 44 118 Q 58 128 88 132 Q 118 134 132 128 Q 140 120 136 108 Q 128 98 98 96 Q 68 96 52 104 Q 44 110 44 118 Z")
                  "velum", layer (Some "translate(0,2)") None ]
          Highlight = [ "open_vowel" ] }

    let private highFrontVowel =
        { Layers =
            Map.ofList
                [ "lips_upper", layer (Some "translate(0,-2) scale(1.02,0.92)") None
                  "lips_lower", layer (Some "translate(0,-1) scale(1.05,0.9)") None
                  "jaw", layer (Some "translate(0,2) rotate(-2 80 160)") None
                  "tongue",
                  layer
                      None
                      (Some "M 52 98 Q 72 72 102 74 Q 128 78 138 96 Q 142 112 128 124 Q 108 132 82 128 Q 58 122 52 98 Z")
                  "velum", layer (Some "translate(0,0)") None ]
          Highlight = [ "high_front_vowel" ] }

    let private roundedBackVowel =
        { Layers =
            Map.ofList
                [ "lips_upper", layer (Some "translate(-4,2) scale(0.92,1.12)") None
                  "lips_lower", layer (Some "translate(-6,4) scale(0.9,1.15)") None
                  "jaw", layer (Some "translate(-2,4)") None
                  "tongue",
                  layer
                      None
                      (Some
                          "M 56 112 Q 78 108 108 114 Q 132 122 140 136 Q 144 150 128 158 Q 102 164 72 158 Q 54 150 52 136 Q 52 122 56 112 Z")
                  "velum", layer (Some "translate(2,0)") None ]
          Highlight = [ "rounded_back_vowel" ] }

    let private interdental =
        { Layers =
            Map.ofList
                [ "lips_upper", layer (Some "translate(0,2)") None
                  "lips_lower", layer (Some "translate(0,4)") None
                  "jaw", layer (Some "translate(0,5)") None
                  "tongue",
                  layer
                      None
                      (Some "M 50 94 Q 60 86 74 84 Q 86 84 90 92 Q 92 98 84 102 Q 72 106 58 104 Q 50 100 50 94 Z")
                  "teeth_upper", layer (Some "translate(0,1)") None ]
          Highlight = [ "interdental"; "theta" ] }

    let private postAlveolar =
        { Layers =
            Map.ofList
                [ "lips_upper", layer (Some "translate(-2,1)") None
                  "lips_lower", layer (Some "translate(-2,3)") None
                  "jaw", layer (Some "translate(0,4)") None
                  "tongue",
                  layer
                      None
                      (Some "M 48 98 Q 64 86 90 88 Q 118 92 130 106 Q 136 118 124 128 Q 102 136 76 132 Q 56 126 48 110 Q 46 102 48 98 Z") ]
          Highlight = [ "post_alveolar" ] }

    let private rhotic =
        { Layers =
            Map.ofList
                [ "lips_upper", layer (Some "translate(0,0)") None
                  "lips_lower", layer (Some "translate(0,2)") None
                  "jaw", layer (Some "translate(0,3)") None
                  "tongue",
                  layer
                      None
                      (Some "M 54 104 Q 70 96 96 100 Q 122 106 132 118 Q 138 128 126 134 Q 104 140 80 136 Q 60 130 54 118 Q 52 110 54 104 Z") ]
          Highlight = [ "rhotic" ] }

    let private nasalVelar =
        { Layers =
            Map.ofList
                [ "lips_upper", layer (Some "translate(0,2)") None
                  "lips_lower", layer (Some "translate(0,4)") None
                  "jaw", layer (Some "translate(0,5)") None
                  "tongue",
                  layer
                      None
                      (Some "M 42 108 Q 56 100 82 102 Q 110 106 124 118 Q 130 128 118 134 Q 96 140 72 136 Q 54 130 42 118 Z")
                  "velum", layer (Some "translate(0,6)") None ]
          Highlight = [ "velar"; "nasal" ] }

    let private enUsMap =
        Map.ofList
            [ "p", bilabialStop
              "b", bilabialStop
              "t", alveolarStop
              "d", alveolarStop
              "k", velarStop
              "ɡ", velarStop
              "g", velarStop
              "æ", openVowel
              "ɛ", highFrontVowel
              "ɪ", highFrontVowel
              "ɑ", openVowel
              "ʊ", roundedBackVowel
              "ɔ", roundedBackVowel
              "θ", interdental
              "ʃ", postAlveolar
              "ɹ", rhotic
              "ŋ", nasalVelar ]

    let tryGetPose (locale: Locale) (ipa: string) =
        if locale <> "en-US" then
            None
        else
            enUsMap.TryFind ipa

    let keyframeForSegment (locale: Locale) (segment: PhonemeSegment) =
        match tryGetPose locale segment.Ipa with
        | Some pose ->
            Some
                { Ipa = segment.Ipa
                  StartMs = segment.StartMs
                  EndMs = segment.EndMs
                  Layers = pose.Layers
                  Highlight = pose.Highlight }
        | None ->
            Some
                { Ipa = segment.Ipa
                  StartMs = segment.StartMs
                  EndMs = segment.EndMs
                  Layers = Map.empty
                  Highlight = [] }

    let keyframesForTimeline (locale: Locale) (phonemes: PhonemeSegment list) =
        phonemes
        |> List.choose (keyframeForSegment locale)
        |> List.filter (fun k -> not k.Layers.IsEmpty)
