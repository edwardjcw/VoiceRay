namespace VoiceRay.Core

/// Rule-based coaching copy for `en-US` compare substitutions (`docs/api.md`).
module CoachingRules =
    type private Rule =
        { Message: string
          HighlightLayers: string list }

    let private rules =
        Map.ofList
            [ // Voiceless / voiced stops (demo words)
              ("t", "d"),
              { Message = "Use a voiceless alveolar stop, not a voiced one."
                HighlightLayers = [ "tongue"; "teeth_upper" ] }
              ("d", "t"),
              { Message = "Use a voiced alveolar stop, not a voiceless one."
                HighlightLayers = [ "tongue"; "teeth_upper" ] }
              ("p", "b"),
              { Message = "Use a voiceless bilabial stop, not a voiced one."
                HighlightLayers = [ "lips_upper"; "lips_lower" ] }
              ("b", "p"),
              { Message = "Use a voiced bilabial stop, not a voiceless one."
                HighlightLayers = [ "lips_upper"; "lips_lower" ] }
              ("k", "ɡ"),
              { Message = "Use a voiceless velar stop, not a voiced one."
                HighlightLayers = [ "tongue"; "velum" ] }
              ("ɡ", "k"),
              { Message = "Use a voiced velar stop, not a voiceless one."
                HighlightLayers = [ "tongue"; "velum" ] }
              // Fricatives / affricates
              ("θ", "ð"),
              { Message = "Use a voiceless interdental fricative (tongue between teeth), not a voiced one."
                HighlightLayers = [ "tongue"; "teeth_upper"; "teeth_lower" ] }
              ("θ", "t"),
              { Message = "Place the tongue between the teeth for /θ/, not at the alveolar ridge."
                HighlightLayers = [ "tongue"; "teeth_upper"; "teeth_lower" ] }
              ("θ", "s"),
              { Message = "Use an interdental /θ/ with the tongue between the teeth, not a sibilant /s/."
                HighlightLayers = [ "tongue"; "teeth_upper"; "teeth_lower" ] }
              ("ʃ", "s"),
              { Message = "Use a post-alveolar /ʃ/ with rounded lips, not a plain alveolar /s/."
                HighlightLayers = [ "lips_upper"; "tongue" ] }
              ("ʃ", "t"),
              { Message = "Use a post-alveolar fricative /ʃ/, not a stop."
                HighlightLayers = [ "lips_upper"; "tongue" ] }
              // Rhotic
              ("ɹ", "w"),
              { Message = "Use an American /ɹ/ with tongue bunched, not a rounded /w/."
                HighlightLayers = [ "tongue" ] }
              ("ɹ", "l"),
              { Message = "Use an /ɹ/ with tongue tip pulled back, not a clear /l/."
                HighlightLayers = [ "tongue" ] }
              // Nasal / velar (think)
              ("ŋ", "n"),
              { Message = "Use a velar nasal /ŋ/ with the tongue back at the velum, not an alveolar /n/."
                HighlightLayers = [ "tongue"; "velum" ] }
              // Vowels (demo set)
              ("æ", "ɛ"),
              { Message = "Open the mouth wider for /æ/ (as in pat), not the closer /ɛ/ vowel."
                HighlightLayers = [ "jaw"; "tongue" ] }
              ("ɛ", "æ"),
              { Message = "Use the closer /ɛ/ vowel, not the more open /æ/."
                HighlightLayers = [ "jaw"; "tongue" ] }
              ("ɪ", "i"),
              { Message = "Use lax /ɪ/ (as in pit), not a tense high vowel."
                HighlightLayers = [ "lips_upper"; "tongue" ] }
              ("ɑ", "ɔ"),
              { Message = "Use an unrounded back /ɑ/ (as in pot), not a rounded /ɔ/."
                HighlightLayers = [ "lips_upper"; "jaw"; "tongue" ] }
              ("ʊ", "u"),
              { Message = "Use lax /ʊ/ (as in put), not a fully rounded /u/."
                HighlightLayers = [ "lips_upper"; "tongue" ] } ]

    let private toMessage (rule: Rule) refIpa userIpa =
        { Message = rule.Message
          HighlightLayers = rule.HighlightLayers
          ReferenceIpa = Some refIpa
          UserIpa = Some userIpa }

    let trySubstitution (locale: Locale) (referenceIpa: string) (userIpa: string) : CoachingMessage option =
        if locale <> "en-US" then
            None
        elif referenceIpa = userIpa then
            None
        else
            match rules.TryFind(referenceIpa, userIpa) with
            | Some rule -> Some(toMessage rule referenceIpa userIpa)
            | None ->
                Some
                    { Message =
                        $"Adjust articulation for /{referenceIpa}/ — you produced /{userIpa}/."
                      HighlightLayers = [ "tongue" ]
                      ReferenceIpa = Some referenceIpa
                      UserIpa = Some userIpa }

    let forSegments (locale: Locale) (segments: CompareSegment list) : CoachingMessage list =
        let mutable seen = Set.empty

        segments
        |> List.choose (function
            | CompareSegment.Substitution(refIpa, userIpa) ->
                let key = refIpa, userIpa

                if Set.contains key seen then
                    None
                else
                    seen <- Set.add key seen

                    match trySubstitution locale refIpa userIpa with
                    | Some msg -> Some msg
                    | None -> None
            | _ -> None)
