namespace VoiceRay.Core

/// Stub pronunciation scores for analyze (full assessment deferred to compare/Azure).
module AnalyzeScoring =
    let private accuracyLabel (score: float) =
        if score >= 90.0 then
            Some "good"
        elif score >= 75.0 then
            Some "fair"
        else
            Some "needs_work"

    /// Scores each aligned user phoneme; stub uses segment length vs expected weight.
    let scorePhonemes (phonemes: PhonemeSegment list) =
        if phonemes.IsEmpty then
            []
        else
            let totalMs =
                phonemes
                |> List.map (fun p -> p.EndMs - p.StartMs)
                |> List.sum
                |> max 1

            phonemes
            |> List.map (fun segment ->
                let span = segment.EndMs - segment.StartMs
                let weight = G2pStub.segmentWeight segment.Ipa
                let totalWeight =
                    phonemes |> List.sumBy (fun p -> G2pStub.segmentWeight p.Ipa)

                let expectedShare = weight / max 0.01 totalWeight
                let actualShare = float span / float totalMs
                let deviation = abs (actualShare - expectedShare)
                let score = max 55.0 (min 98.0 (92.0 - deviation * 120.0))

                { Ipa = segment.Ipa
                  Score = score
                  Accuracy = accuracyLabel score })
