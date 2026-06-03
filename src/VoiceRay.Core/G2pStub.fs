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
