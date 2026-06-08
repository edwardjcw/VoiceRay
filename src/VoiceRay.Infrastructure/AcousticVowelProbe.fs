namespace VoiceRay.Infrastructure

open System
open System.Collections.Concurrent
open VoiceRay.Core

/// Infers spoken vowels from user audio so analyze/compare reflect pronunciation, not just expected text.
module AcousticVowelProbe =
    type VowelFeatures =
        { CentroidHz: float
          LowBand: float
          MidBand: float
          HighBand: float
          Rms: float }

    let private sampleRate = 16000
    let private minVowelRms = 0.008
    let private vowelWindowMs = 140
    let private exemplarCache = ConcurrentDictionary<string, VowelFeatures>()

    let private pVtWordForVowel =
        Map.ofList [ "æ", "pat"; "ɛ", "pet"; "ɪ", "pit"; "ɑ", "pot"; "ʊ", "put" ]

    let private staticProfiles =
        Map.ofList
            [ "æ",
              { CentroidHz = 950.0
                LowBand = 0.52
                MidBand = 0.32
                HighBand = 0.16
                Rms = 0.12 }
              "ɛ",
              { CentroidHz = 1480.0
                LowBand = 0.40
                MidBand = 0.45
                HighBand = 0.15
                Rms = 0.12 }
              "ɪ",
              { CentroidHz = 2180.0
                LowBand = 0.22
                MidBand = 0.33
                HighBand = 0.45
                Rms = 0.11 }
              "ɑ",
              { CentroidHz = 880.0
                LowBand = 0.64
                MidBand = 0.26
                HighBand = 0.10
                Rms = 0.13 }
              "ʊ",
              { CentroidHz = 1320.0
                LowBand = 0.46
                MidBand = 0.40
                HighBand = 0.14
                Rms = 0.11 }
              "ɔ",
              { CentroidHz = 1050.0
                LowBand = 0.50
                MidBand = 0.36
                HighBand = 0.14
                Rms = 0.12 }
              "i",
              { CentroidHz = 2550.0
                LowBand = 0.14
                MidBand = 0.24
                HighBand = 0.62
                Rms = 0.10 } ]

    let private goertzelPower (samples: float[]) (targetHz: float) =
        if samples.Length = 0 then
            0.0
        else
            let omega = 2.0 * Math.PI * targetHz / float sampleRate
            let coeff = 2.0 * cos omega
            let mutable s1 = 0.0
            let mutable s2 = 0.0

            for x in samples do
                let s0 = x + coeff * s1 - s2
                s2 <- s1
                s1 <- s0

            s1 * s1 + s2 * s2 - coeff * s1 * s2

    let private extractFeatures (samples: int16[]) (startIdx: int) (endIdx: int) =
        if endIdx <= startIdx || endIdx - startIdx < 32 then
            None
        else
            let slice =
                samples.[startIdx .. endIdx - 1]
                |> Array.map (fun s -> float s / 32768.0)

            let rms =
                slice
                |> Array.map (fun x -> x * x)
                |> Array.average
                |> sqrt

            if rms < minVowelRms then
                None
            else
                let freqs = [| 400.0; 700.0; 1100.0; 1600.0; 2200.0; 2800.0 |]

                let powers =
                    freqs |> Array.map (goertzelPower slice)

                let total = powers |> Array.sum |> max 1e-9
                let centroid = (Array.zip freqs powers |> Array.sumBy (fun (f, p) -> f * p)) / total

                Some
                    { CentroidHz = centroid
                      LowBand = (powers.[0] + powers.[1]) / total
                      MidBand = (powers.[2] + powers.[3]) / total
                      HighBand = (powers.[4] + powers.[5]) / total
                      Rms = rms }

    let private featureDistance (a: VowelFeatures) (b: VowelFeatures) =
        let centroidTerm = (a.CentroidHz - b.CentroidHz) / 900.0

        centroidTerm * centroidTerm * 2.5
        + (a.LowBand - b.LowBand) ** 2.0
        + (a.MidBand - b.MidBand) ** 2.0
        + (a.HighBand - b.HighBand) ** 2.0

    let private msToSample (ms: int) = ms * sampleRate / 1000

    let private vowelSegmentIndices (phonemes: PhonemeSegment list) =
        phonemes |> List.mapi (fun i p -> i, p) |> List.filter (fun (_, p) -> G2pStub.isVowel p.Ipa)

    /// Finds the highest-energy window near the expected vowel slot (G2P timing is often wrong for live speech).
    let private findVowelWindow (samples: int16[]) (segment: PhonemeSegment) =
        let windowSamples = max 48 (msToSample vowelWindowMs)
        let approxStart = msToSample segment.StartMs
        let approxEnd = msToSample segment.EndMs
        let pad = sampleRate / 4
        let searchStart = max 0 (approxStart - pad)
        let searchEnd = min samples.Length (max (approxEnd + pad) (searchStart + windowSamples))

        if searchEnd <= searchStart + windowSamples then
            approxStart, min samples.Length (approxStart + windowSamples)
        else
            let mutable bestStart = searchStart
            let mutable bestRms = 0.0

            for start in searchStart .. (searchEnd - windowSamples) do
                match extractFeatures samples start (start + windowSamples) with
                | None -> ()
                | Some feat ->
                    if feat.Rms > bestRms then
                        bestRms <- feat.Rms
                        bestStart <- start

            bestStart, bestStart + windowSamples

    let private exemplarWord (practiceWord: string) (ipa: string) =
        match pVtWordForVowel.TryFind ipa with
        | Some w -> w
        | None -> G2pStub.normalizeWord practiceWord

    let private tryExemplarFeatures (piperOptions: PiperOptions) (practiceWord: string) (ipa: string) =
        let word = exemplarWord practiceWord ipa
        let cacheKey = $"{word}:{ipa}"

        match exemplarCache.TryGetValue cacheKey with
        | true, cached -> Some cached
        | false, _ ->
            if not (PiperOptions.isConfigured piperOptions) then
                None
            else
                match PiperTts.synthesize piperOptions word with
                | Error _ -> None
                | Ok wavBytes ->
                    match AudioNormalizer.tryParsePcm wavBytes with
                    | None -> None
                    | Some pcm ->
                        let durationMs =
                            WavDuration.tryGetDurationMs wavBytes |> Option.defaultValue 320

                        match G2pStub.tryLookup "en-US" word with
                        | None -> None
                        | Some g2p ->
                            let timeline = G2pStub.buildTimeline g2p.IpaSymbols durationMs

                            match timeline |> List.tryFind (fun p -> p.Ipa = ipa) with
                            | None -> None
                            | Some segment ->
                                let startIdx = msToSample segment.StartMs
                                let endIdx = msToSample segment.EndMs

                                match extractFeatures pcm.Samples startIdx endIdx with
                                | None -> None
                                | Some features ->
                                    exemplarCache.[cacheKey] <- features
                                    Some features

    let private scoreCandidate
        (user: VowelFeatures)
        (ipa: string)
        (practiceWord: string)
        (piperOptions: PiperOptions)
        =
        let staticDist =
            match staticProfiles.TryFind ipa with
            | Some profile -> featureDistance user profile
            | None -> Double.MaxValue / 2.0

        let exemplarDist =
            match tryExemplarFeatures piperOptions practiceWord ipa with
            | Some exemplar -> featureDistance user exemplar
            | None -> Double.MaxValue / 2.0

        min staticDist exemplarDist

    let private replaceVowel (phonemes: PhonemeSegment list) (index: int) (newIpa: string) =
        phonemes
        |> List.mapi (fun i segment ->
            if i = index then
                { segment with Ipa = newIpa }
            else
                segment)

    /// Adjusts aligned phonemes when acoustic evidence favors a different vowel than G2P expects.
    let refinePhonemes
        (normalizedWav: byte[])
        (locale: Locale)
        (practiceWord: string)
        (phonemes: PhonemeSegment list)
        (piperOptions: PiperOptions)
        =
        if locale <> "en-US" then
            phonemes
        else
            let candidates = G2pStub.vowelCandidates practiceWord

            if candidates.Length <= 1 then
                phonemes
            else
                match AudioNormalizer.tryParsePcm normalizedWav with
                | None -> phonemes
                | Some pcm ->
                    let vowelSlots = vowelSegmentIndices phonemes

                    vowelSlots
                    |> List.fold (fun current (index, segment) ->
                        let startIdx, endIdx = findVowelWindow pcm.Samples segment

                        match extractFeatures pcm.Samples startIdx endIdx with
                        | None -> current
                        | Some userFeatures ->
                            let expectedIpa = segment.Ipa

                            let ranked =
                                candidates
                                |> List.map (fun ipa -> ipa, scoreCandidate userFeatures ipa practiceWord piperOptions)
                                |> List.sortBy snd

                            match ranked with
                            | [] -> current
                            | (bestIpa, bestScore) :: rest ->
                                if bestIpa = expectedIpa then
                                    current
                                else
                                    let expectedScore =
                                        ranked
                                        |> List.tryFind (fun (ipa, _) -> ipa = expectedIpa)
                                        |> Option.map snd
                                        |> Option.defaultValue (bestScore + 1.0)

                                    let margin =
                                        match rest with
                                        | (_, secondScore) :: _ -> secondScore - bestScore
                                        | _ -> 0.0

                                    if bestScore < expectedScore * 0.95 && margin > 0.025 then
                                        replaceVowel current index bestIpa
                                    else
                                        current) phonemes
