module ReferenceServiceTests

open System
open System.IO
open VoiceRay.Core
open VoiceRay.Infrastructure
open Xunit

let private repoRoot =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

let private piperOptions () =
    { Executable = Path.Combine(repoRoot, "models", "piper", "bin", "piper", "piper.exe")
      VoiceModel = Path.Combine(repoRoot, "models", "piper", "voices", "en_US-lessac-medium.onnx")
      MediaRoot = Path.Combine(repoRoot, "src", "VoiceRay.Api", "wwwroot", "media", "reference") }

/// Even-spread reference (no forced alignment): provider Whisper keeps timing deterministic
/// regardless of whether the wav2vec2 model is provisioned on the host.
let private evenSpreadAlignment () =
    { Provider = AlignmentProvider.Whisper
      WhisperCacheDir = None
      MfaWorkerUrl = None
      Wav2Vec2Variant = Wav2Vec2Provisioner.defaultVariant }

let private wav2vec2Alignment () =
    { evenSpreadAlignment () with
        Provider = AlignmentProvider.Wav2Vec2 }

let private referenceService (piper: PiperOptions) =
    ReferenceService(piper, evenSpreadAlignment (), repoRoot)

[<Fact>]
let ``ReferenceService rejects unknown demo word`` () =
    let service = referenceService (piperOptions ())
    match service.Generate { Text = "xyzzy"; Locale = "en-US" } with
    | Error ReferenceServiceError.G2pUnavailable -> ()
    | other -> Assert.Fail($"Expected G2pUnavailable, got {other}")

[<Fact>]
let ``ReferenceService returns keyframes for pat when Piper configured`` () =
    let options = piperOptions ()

    if not (PiperOptions.isConfigured options) then
        ()
    else
        let service = referenceService options

        match service.Generate { Text = "pat"; Locale = "en-US" } with
        | Error err -> Assert.Fail($"Expected OK, got {err}")
        | Ok response ->
            Assert.Equal("pæt", response.IpaDisplay)
            Assert.Equal(3, response.Phonemes.Length)
            Assert.True(response.Keyframes.Length >= 2)
            Assert.True(response.AudioUrl.IsSome || response.AudioBase64.IsSome)

let private demoWords =
    [ "pat"; "pet"; "pit"; "pot"; "put"; "cat"; "dog"; "think"; "red"; "ship" ]

[<Fact>]
let ``ReferenceService covers all demo words when Piper configured`` () =
    let options = piperOptions ()

    if not (PiperOptions.isConfigured options) then
        ()
    else
        let service = referenceService options

        for word in demoWords do
            match service.Generate { Text = word; Locale = "en-US" } with
            | Error err -> Assert.Fail($"Word '{word}' failed: {err}")
            | Ok response ->
                Assert.False(List.isEmpty response.Phonemes)
                Assert.False(List.isEmpty response.Keyframes)
                Assert.False(String.IsNullOrWhiteSpace response.IpaDisplay)

[<Fact>]
let ``ReferenceService falls back to even-spread when wav2vec2 model is absent`` () =
    let options = piperOptions ()

    if not (PiperOptions.isConfigured options) || Wav2Vec2Phoneme.isReady repoRoot then
        () // covered by the forced-alignment test below when the model is present
    else
        // Provider is Wav2Vec2 but the model is not provisioned → must transparently fall back.
        let service = ReferenceService(options, wav2vec2Alignment (), repoRoot)

        match service.Generate { Text = "pat"; Locale = "en-US" } with
        | Error err -> Assert.Fail($"Expected OK, got {err}")
        | Ok response ->
            Assert.Equal(3, response.Phonemes.Length)

            let expected =
                match G2pStub.tryLookup "en-US" "pat" with
                | Some g2p ->
                    let durMs = (response.Phonemes |> List.map (fun p -> p.EndMs) |> List.max)
                    G2pStub.buildTimeline g2p.IpaSymbols durMs
                | None ->
                    Assert.Fail("G2P unavailable for pat")
                    []

            // Even-spread covers [0, duration) contiguously; the fallback must match it exactly.
            Assert.Equal<PhonemeSegment list>(expected, response.Phonemes)

[<Fact>]
let ``ReferenceService forced-aligns pat against Piper audio when model provisioned`` () =
    let options = piperOptions ()

    if not (PiperOptions.isConfigured options) || not (Wav2Vec2Phoneme.isReady repoRoot) then
        () // need both Piper synthesis and the wav2vec2 model on this host
    else
        Wav2Vec2Phoneme.resetSession ()

        try
            let service = ReferenceService(options, wav2vec2Alignment (), repoRoot)

            match service.Generate { Text = "pat"; Locale = "en-US" } with
            | Error err -> Assert.Fail($"Expected OK, got {err}")
            | Ok response ->
                Assert.Equal("pæt", response.IpaDisplay)
                Assert.Equal(3, response.Phonemes.Length)

                // The aligned segments must remain ordered and in-range.
                let ordered =
                    response.Phonemes
                    |> List.pairwise
                    |> List.forall (fun (a, b) -> a.StartMs <= a.EndMs && a.EndMs <= b.StartMs)

                Assert.True(ordered, "aligned phonemes should be ordered and non-overlapping")

                // Forced alignment must differ from the deterministic even-spread timeline:
                // real acoustic boundaries are not the weighted equal split over the full clip.
                let durMs = (response.Phonemes |> List.map (fun p -> p.EndMs) |> List.max)

                let evenSpread =
                    match G2pStub.tryLookup "en-US" "pat" with
                    | Some g2p -> G2pStub.buildTimeline g2p.IpaSymbols durMs
                    | None -> []

                Assert.NotEqual<PhonemeSegment list>(evenSpread, response.Phonemes)
        finally
            Wav2Vec2Phoneme.resetSession ()
