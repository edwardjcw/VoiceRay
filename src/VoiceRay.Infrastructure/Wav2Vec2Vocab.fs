namespace VoiceRay.Infrastructure

open System
open System.Text.Json
open VoiceRay.Core

/// Parses the wav2vec2 espeak `vocab.json` and maps model IPA tokens onto the
/// VoiceRay en-US phoneme inventory used by `G2pStub`/`PoseMap`/compare.
module Wav2Vec2Vocab =

    type Vocab =
        { IdToToken: Map<int, string>
          TokenToId: Map<string, int>
          BlankId: int
          SpecialIds: Set<int> }

    let private isSpecialToken (token: string) =
        token.StartsWith "<" && token.EndsWith ">"

    /// Maps an espeak IPA token to the VoiceRay inventory: drops length/stress/
    /// palatalization marks and folds a few near-equivalents onto demo symbols.
    /// Nasal vowels (e.g. ɑ̃ ɛ̃ ɔ̃ œ̃) are preserved so non-English (French) words map
    /// to their own poses instead of collapsing onto en-US oral vowels.
    let normalizeIpa (token: string) : string =
        let stripped =
            token
                .Trim()
                .Replace("ː", "")
                .Replace("ˈ", "")
                .Replace("ˌ", "")
                .Replace("ʲ", "")

        match stripped with
        | "ɐ" -> "ə" // near-open central → schwa
        | "ᵻ" -> "ɪ" // barred-i (reduced) → ɪ
        | "ɝ" -> "ɚ" // stressed rhotic schwa → ɚ
        | "g" -> "ɡ" // ASCII g → IPA script g (matches G2pStub)
        | other -> other

    /// Parses vocab JSON ("token" -> id). Returns None on malformed input.
    let tryParse (json: string) : Vocab option =
        try
            use doc = JsonDocument.Parse json
            let root = doc.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                None
            else
                let pairs =
                    [ for p in root.EnumerateObject() -> p.Name, p.Value.GetInt32() ]

                if List.isEmpty pairs then
                    None
                else
                    let tokenToId = pairs |> Map.ofList
                    let idToToken = pairs |> List.map (fun (t, i) -> i, t) |> Map.ofList

                    let blankId =
                        match tokenToId.TryFind "<pad>" with
                        | Some id -> id
                        | None -> 0

                    let specialIds =
                        pairs
                        |> List.choose (fun (t, i) -> if isSpecialToken t then Some i else None)
                        |> Set.ofList

                    Some
                        { IdToToken = idToToken
                          TokenToId = tokenToId
                          BlankId = blankId
                          SpecialIds = specialIds }
        with _ ->
            None

    let tryLoad (path: string) : Vocab option =
        if System.IO.File.Exists path then
            tryParse (System.IO.File.ReadAllText path)
        else
            None

    /// Converts a decoded token span to a normalized inventory IPA symbol, or
    /// `None` for blanks/specials/empty tokens.
    let spanToIpa (vocab: Vocab) (span: Ctc.TokenSpan) : string option =
        if vocab.SpecialIds.Contains span.TokenId then
            None
        else
            match vocab.IdToToken.TryFind span.TokenId with
            | None -> None
            | Some token ->
                let ipa = normalizeIpa token
                if String.IsNullOrWhiteSpace ipa then None else Some ipa
