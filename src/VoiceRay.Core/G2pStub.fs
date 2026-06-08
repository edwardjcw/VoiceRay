namespace VoiceRay.Core

open System

/// CMU-style G2P stub for the pedagogical demo word set (`docs/status.md`, `en-US`).
module G2pStub =
    type G2pResult =
        { IpaSymbols: string list
          IpaDisplay: string }

    let private demoLexicon =
        [ "pat", ([ "p"; "æ"; "t" ], "pæt")
          "pet", ([ "p"; "ɛ"; "t" ], "pɛt")
          "pit", ([ "p"; "ɪ"; "t" ], "pɪt")
          "pot", ([ "p"; "ɑ"; "t" ], "pɑt")
          "put", ([ "p"; "ʊ"; "t" ], "pʊt")
          "cat", ([ "k"; "æ"; "t" ], "kæt")
          "dog", ([ "d"; "ɔ"; "ɡ" ], "dɔɡ")
          "think", ([ "θ"; "ɪ"; "ŋ" ], "θɪŋ")
          "red", ([ "ɹ"; "ɛ"; "d" ], "ɹɛd")
          "ship", ([ "ʃ"; "ɪ"; "p" ], "ʃɪp") ]
        |> Map.ofList

    let normalizeWord (text: string) =
        if String.IsNullOrWhiteSpace text then
            ""
        else
            text.Trim().ToLowerInvariant()

    let private cleanToken (token: string) =
        token.Trim().TrimEnd([| '.'; ','; '!'; '?'; ';'; ':'; '"'; ''' |])

    let private transcriptTokens (transcript: string) =
        transcript.Trim().ToLowerInvariant().Split([| ' '; '\t'; '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map cleanToken
        |> Array.filter (fun token -> not (String.IsNullOrWhiteSpace token))

    /// Words in the same consonant shell (e.g. pat/pet/pit/pot/put) for whole-word acoustic matching.
    let demoWordGroup (text: string) =
        match normalizeWord text with
        | w when List.contains w [ "pat"; "pet"; "pit"; "pot"; "put" ] ->
            Some [ "pat"; "pet"; "pit"; "pot"; "put" ]
        | _ -> None

    /// Maps ASR output to a demo lexicon word (first token, punctuation stripped).
    let tryParseTranscriptWord (transcript: string) =
        transcriptTokens transcript
        |> Array.tryPick (fun token ->
            if demoLexicon.ContainsKey token then
                Some token
            else
                None)

    /// Picks the best demo word for a practice session (prefers minimal-pair siblings over the prompt word).
    let tryParseTranscriptForPractice (practiceWord: string) (transcript: string) =
        let practice = normalizeWord practiceWord
        let tokens = transcriptTokens transcript |> Array.toList

        if tokens.IsEmpty then
            None
        else
            let group =
                demoWordGroup practiceWord
                |> Option.defaultValue (demoLexicon |> Map.toList |> List.map fst)

            let inGroup word = List.contains word group

            tokens
            |> List.tryFind (fun token -> inGroup token && token <> practice)
            |> Option.orElse (
                tokens
                |> List.tryFind (fun token -> demoLexicon.ContainsKey token && token <> practice)
            )
            |> Option.orElse (tryParseTranscriptWord transcript)

    let tryLookup (locale: Locale) (text: string) =
        if locale <> "en-US" then
            None
        else
            let word = normalizeWord text

            if String.IsNullOrWhiteSpace word then
                None
            else
                demoLexicon.TryFind word
                |> Option.map (fun (symbols, display) ->
                    { IpaSymbols = symbols
                      IpaDisplay = display })

    let private vowelSymbols =
        Set
            [ "æ"
              "ɛ"
              "ɪ"
              "ɑ"
              "ʊ"
              "ɔ"
              "i"
              "u"
              "ə"
              "ɚ"
              "ɝ" ]

    let isVowel (ipa: string) = vowelSymbols.Contains ipa

    let demoLexiconWords () = demoLexicon |> Map.toList |> List.map fst

    /// Candidate vowels to probe when inferring what the user actually spoke (minimal-pair groups).
    let vowelCandidates (text: string) =
        match normalizeWord text with
        | w when List.contains w [ "pat"; "pet"; "pit"; "pot"; "put" ] ->
            [ "æ"; "ɛ"; "ɪ"; "ɑ"; "ʊ" ]
        | "cat" -> [ "æ"; "ɑ"; "ɛ" ]
        | "dog" -> [ "ɔ"; "ɑ"; "ʊ" ]
        | "think" -> [ "ɪ"; "i"; "ɛ" ]
        | "red" -> [ "ɛ"; "æ"; "ɪ" ]
        | "ship" -> [ "ɪ"; "i"; "ɛ" ]
        | w ->
            match tryLookup "en-US" w with
            | Some g2p -> g2p.IpaSymbols |> List.filter isVowel
            | None -> []

    let segmentWeight (ipa: string) =
        match ipa with
        | "æ" | "ɛ" | "ɪ" | "ɑ" | "ʊ" | "ɔ" -> 2.5
        | _ -> 1.0

    /// Builds timed phoneme segments that span `[0, durationMs)`.
    let buildTimeline (ipaSymbols: string list) (durationMs: int) =
        if ipaSymbols.IsEmpty then
            []
        else
            let weights = ipaSymbols |> List.map segmentWeight
            let total = List.sum weights
            let mutable cursor = 0

            ipaSymbols
            |> List.zip weights
            |> List.map (fun (weight, ipa) ->
                let span =
                    if total <= 0.0 then
                        durationMs / ipaSymbols.Length
                    else
                        int (float durationMs * weight / total)

                let span = max 1 span
                let startMs = cursor
                let endMs = min durationMs (cursor + span)
                cursor <- endMs

                { Ipa = ipa
                  StartMs = startMs
                  EndMs = endMs })
            |> fun segments ->
                match segments with
                | [] -> segments
                | _ ->
                    let last = List.last segments
                    let init = segments |> List.take (segments.Length - 1)
                    init @ [ { last with EndMs = durationMs } ]
