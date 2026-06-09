module PitPetVowelTests

open System
open System.IO
open VoiceRay.Core
open VoiceRay.Infrastructure
open Xunit

// Verifies the wav2vec2 model discriminates the two minimal-pair front vowels in the recorded
// fixtures, and pins what the model ACTUALLY produces (acoustically honest assertions):
//
//   pit-practice-pat.wav -> /e/  (close-mid front; the model resolves the speaker's lax /ɪ/ as /e/)
//   pet-practice-pat.wav -> /ɛ/  (open-mid front)
//
// Finding: every variant tested (model / model_fp16 / model_quantized) hears the "pit" nucleus as
// /e/, not /ɪ/ — i.e. the /ɪ/→/e/ reading is acoustic, not a quantization artifact. What matters
// for coaching is that the two nuclei are DISTINCT and both differ from the prompted /æ/, which
// holds: pit→/e/ ≠ pet→/ɛ/ ≠ /æ/. See docs/status.md for the full variant comparison.
//
// Every test is gated on Wav2Vec2Phoneme.isReady (skips when the model is absent), exactly like
// the other wav2vec2 tests.

let private repoRoot () =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

let private fixture name =
    Path.Combine(repoRoot (), "tests", "fixtures", name)

let private pitFixture () = fixture "pit-practice-pat.wav"
let private petFixture () = fixture "pet-practice-pat.wav"

let private piperOptions () =
    let root = repoRoot ()

    { Executable = Path.Combine(root, "models", "piper", "bin", "piper", "piper.exe")
      VoiceModel = Path.Combine(root, "models", "piper", "voices", "en_US-lessac-medium.onnx")
      MediaRoot = Path.Combine(root, "wwwroot", "media", "reference") }

let private wav2vec2Alignment () =
    { Provider = AlignmentProvider.Wav2Vec2
      WhisperCacheDir = None
      MfaWorkerUrl = None
      Wav2Vec2Variant = Wav2Vec2Provisioner.defaultVariant }

/// IPA monophthong inventory the model can emit (en-US normalized). Used to pick the nucleus.
let private vowels =
    set [ "i"; "ɪ"; "e"; "ɛ"; "æ"; "ə"; "ɚ"; "ʌ"; "ɑ"; "ɔ"; "o"; "ʊ"; "u"; "ɜ"; "ɒ"; "a"; "ɐ" ]

let private modelReady () =
    Wav2Vec2Phoneme.isReady (repoRoot ())
    && File.Exists(pitFixture ())
    && File.Exists(petFixture ())

let private analyzeVowels (wavPath: string) =
    let service =
        AnalyzeService(wav2vec2Alignment (), piperOptions (), repoRoot ())

    let wav = File.ReadAllBytes wavPath

    match service.Analyze(wav, "pat", "en-US") with
    | Error err -> failwith $"Analyze failed for {Path.GetFileName wavPath}: {err}"
    | Ok response ->
        let ipas = response.Phonemes |> List.map (fun p -> p.Ipa)
        let vowelsHeard = ipas |> List.filter vowels.Contains
        response, ipas, vowelsHeard

[<Fact>]
let ``wav2vec2 hears a distinct front vowel (e) for the pit fixture, not the prompted ae`` () =
    if not (modelReady ()) then
        () // wav2vec2 model or fixtures unavailable in this environment
    else
        Wav2Vec2Phoneme.resetSession ()

        try
            let response, ipas, vowelsHeard = analyzeVowels (pitFixture ())
            let all = String.concat "," ipas
            let heard = String.concat "," vowelsHeard

            Assert.Equal("wav2vec2", response.Metadata.AlignmentEngine)

            // The model resolves the speaker's lax /ɪ/ as the close-mid front vowel /e/.
            Assert.True(
                List.contains "e" vowelsHeard,
                $"expected the close-mid front vowel /e/ for pit; heard vowels=[{heard}] all=[{all}]"
            )

            Assert.False(
                List.contains "æ" vowelsHeard,
                $"should not hear the prompted /æ/ for pit; all=[{all}]"
            )
        finally
            Wav2Vec2Phoneme.resetSession ()

[<Fact>]
let ``wav2vec2 hears the mid front vowel ee for the pet fixture, not the prompted ae`` () =
    if not (modelReady ()) then
        ()
    else
        Wav2Vec2Phoneme.resetSession ()

        try
            let response, ipas, vowelsHeard = analyzeVowels (petFixture ())
            let all = String.concat "," ipas
            let heard = String.concat "," vowelsHeard

            Assert.Equal("wav2vec2", response.Metadata.AlignmentEngine)

            Assert.True(
                List.contains "ɛ" vowelsHeard,
                $"expected the open-mid front vowel /ɛ/ for pet; heard vowels=[{heard}] all=[{all}]"
            )

            Assert.False(
                List.contains "æ" vowelsHeard,
                $"should not hear the prompted /æ/ for pet; all=[{all}]"
            )
        finally
            Wav2Vec2Phoneme.resetSession ()

[<Fact>]
let ``wav2vec2 separates the pit and pet front vowels`` () =
    if not (modelReady ()) then
        ()
    else
        Wav2Vec2Phoneme.resetSession ()

        try
            let _, pitAll, pitVowels = analyzeVowels (pitFixture ())
            let _, petAll, petVowels = analyzeVowels (petFixture ())

            Assert.NotEmpty pitVowels
            Assert.NotEmpty petVowels

            let pitV = String.concat "," pitVowels
            let petV = String.concat "," petVowels
            let pitA = String.concat "," pitAll
            let petA = String.concat "," petAll

            // The two minimal-pair nuclei must not collapse to the same vowel.
            Assert.True(
                pitVowels <> petVowels,
                $"pit vowels=[{pitV}] (all [{pitA}]) must differ from pet vowels=[{petV}] (all [{petA}])"
            )
        finally
            Wav2Vec2Phoneme.resetSession ()

let private compareAgainstPat (userPhonemes: PhonemeSegment list) =
    let reference =
        match G2pStub.tryLookup "en-US" "pat" with
        | Some g2p -> G2pStub.buildTimeline g2p.IpaSymbols 280
        | None -> failwith "G2P unavailable for pat"

    match
        CompareService()
            .Compare
            { Locale = "en-US"
              ReferencePhonemes = reference
              UserPhonemes = userPhonemes }
    with
    | Error err -> failwith $"Compare failed: {err}"
    | Ok result -> result

[<Fact>]
let ``wav2vec2 pit vs pat reference yields an ae substitution to e`` () =
    if not (modelReady ()) then
        ()
    else
        Wav2Vec2Phoneme.resetSession ()

        try
            let service =
                AnalyzeService(wav2vec2Alignment (), piperOptions (), repoRoot ())

            let wav = File.ReadAllBytes(pitFixture ())

            match service.Analyze(wav, "pat", "en-US") with
            | Error err -> Assert.Fail($"Analyze failed: {err}")
            | Ok user ->
                let result = compareAgainstPat user.Phonemes
                Assert.Contains(CompareSegment.Substitution("æ", "e"), result.Segments)
        finally
            Wav2Vec2Phoneme.resetSession ()

[<Fact>]
let ``wav2vec2 pet vs pat reference coaches ae to ɛ`` () =
    if not (modelReady ()) then
        ()
    else
        Wav2Vec2Phoneme.resetSession ()

        try
            let service =
                AnalyzeService(wav2vec2Alignment (), piperOptions (), repoRoot ())

            let wav = File.ReadAllBytes(petFixture ())

            match service.Analyze(wav, "pat", "en-US") with
            | Error err -> Assert.Fail($"Analyze failed: {err}")
            | Ok user ->
                let result = compareAgainstPat user.Phonemes
                Assert.Contains(CompareSegment.Substitution("æ", "ɛ"), result.Segments)
        finally
            Wav2Vec2Phoneme.resetSession ()
