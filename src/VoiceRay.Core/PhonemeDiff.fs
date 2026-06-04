namespace VoiceRay.Core

/// Greedy left-to-right IPA alignment between reference and user phoneme timelines.
module PhonemeDiff =
    let private ipaSymbols (segments: PhonemeSegment list) =
        segments |> List.map (fun s -> s.Ipa)

    /// Aligns phoneme IPA strings with match / substitution / omission / insertion segments.
    let alignGreedy (reference: PhonemeSegment list) (user: PhonemeSegment list) : CompareSegment list =
        let ref = ipaSymbols reference
        let usr = ipaSymbols user

        let rec loop i j acc =
            if i >= ref.Length && j >= usr.Length then
                List.rev acc
            elif i >= ref.Length then
                loop i (j + 1) (CompareSegment.Insertion usr.[j] :: acc)
            elif j >= usr.Length then
                loop (i + 1) j (CompareSegment.Omission ref.[i] :: acc)
            elif ref.[i] = usr.[j] then
                loop (i + 1) (j + 1) (CompareSegment.Match :: acc)
            elif i + 1 < ref.Length && ref.[i + 1] = usr.[j] then
                loop (i + 1) j (CompareSegment.Omission ref.[i] :: acc)
            elif j + 1 < usr.Length && ref.[i] = usr.[j + 1] then
                loop i (j + 1) (CompareSegment.Insertion usr.[j] :: acc)
            else
                loop (i + 1) (j + 1) (CompareSegment.Substitution(ref.[i], usr.[j]) :: acc)

        loop 0 0 []
