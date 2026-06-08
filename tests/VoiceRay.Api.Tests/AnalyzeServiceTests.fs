module AnalyzeServiceTests

open System
open System.IO
open VoiceRay.Core
open VoiceRay.Infrastructure
open Xunit

let private repoRoot () =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

let private alignmentOptions () =
    { Provider = AlignmentProvider.Whisper
      WhisperCacheDir = Some(Path.Combine(repoRoot (), "models", "whisper"))
      MfaWorkerUrl = None }

/// Synthetic sine-wave clips are not valid Whisper input; keep these on acoustic inference only.
let private acousticOnlyAlignmentOptions () =
    { alignmentOptions () with
        WhisperCacheDir = Some "__voiceray_missing_whisper_cache__" }

let private piperOptions () =
    let root = repoRoot ()

    { Executable = Path.Combine(root, "models", "piper", "bin", "piper", "piper.exe")
      VoiceModel = Path.Combine(root, "models", "piper", "voices", "en_US-lessac-medium.onnx")
      MediaRoot = Path.Combine(root, "wwwroot", "media", "reference") }

let private piperConfigured () =
    let opts = piperOptions ()
    PiperOptions.isConfigured opts

let private contentRoot () = repoRoot ()

let private wav16kMono () =
    let samples = Array.init 3200 (fun i -> int16 (sin (float i / 50.0) * 4000.0))
    WavTestHelpers.encodeMonoPcm 16000 1 samples

/// p–V–t shaped clip with vowel energy dominated by `vowelHz` (æ ~900 Hz, ɪ ~2400 Hz).
let private pVtWav (vowelHz: float) =
    let sampleRate = 16000
    let durationMs = 400
    let sampleCount = sampleRate * durationMs / 1000

    let samples =
        Array.init sampleCount (fun i ->
            let t = float i / float sampleRate
            let ms = t * 1000.0

            let amp =
                if ms < 70.0 then
                    0.35 * sin (2.0 * Math.PI * 180.0 * t)
                elif ms < 260.0 then
                    0.85 * sin (2.0 * Math.PI * vowelHz * t)
                    + 0.25 * sin (2.0 * Math.PI * (vowelHz * 0.55) * t)
                else
                    0.3 * sin (2.0 * Math.PI * 3200.0 * t)

            let scaled = int (amp * 14000.0)
            int16 (Math.Clamp(scaled, -32768, 32767)))

    WavTestHelpers.encodeMonoPcm sampleRate 1 samples

[<Fact>]
let ``AnalyzeService rejects unknown word`` () =
    let service = AnalyzeService(alignmentOptions (), piperOptions (), contentRoot ())
    let wav = wav16kMono ()

    match service.Analyze(wav, "xyzzy", "en-US") with
    | Error AnalyzeServiceError.G2pUnavailable -> ()
    | other -> Assert.Fail($"Expected G2pUnavailable, got {other}")

[<Fact>]
let ``AnalyzeService returns phonemes keyframes scores and metadata`` () =
    let service = AnalyzeService(alignmentOptions (), piperOptions (), contentRoot ())
    let wav = wav16kMono ()

    match service.Analyze(wav, "pat", "en-US") with
    | Error err -> Assert.Fail($"Expected OK, got {err}")
    | Ok response ->
        Assert.Equal(3, response.Phonemes.Length)
        Assert.True(response.Keyframes.Length >= 2)
        Assert.Equal(3, response.Scores.Length)
        Assert.True(response.AudioEcho.IsSome)
        Assert.Equal("whisper-stub", response.Metadata.AlignmentEngine)
        Assert.True(
            response.Metadata.DeviceBanner.Contains("CPU")
            || response.Metadata.DeviceBanner.Contains("CUDA"))

[<Fact>]
let ``AnalyzeService uses MFA stub when Whisper cache unavailable and provider Whisper`` () =
    let options =
        { alignmentOptions () with
            WhisperCacheDir = Some "__missing_whisper_cache__" }

    let service = AnalyzeService(options, piperOptions (), contentRoot ())
    let wav = wav16kMono ()

    match service.Analyze(wav, "pat", "en-US") with
    | Error err -> Assert.Fail($"Expected OK, got {err}")
    | Ok response -> Assert.Equal("mfa-stub", response.Metadata.AlignmentEngine)

[<Fact>]
let ``AnalyzeService detects pat practiced with pit-like vowel`` () =
    let service = AnalyzeService(acousticOnlyAlignmentOptions (), piperOptions (), contentRoot ())
    let wav = pVtWav 2400.0

    match service.Analyze(wav, "pat", "en-US") with
    | Error err -> Assert.Fail($"Expected OK, got {err}")
    | Ok response ->
        Assert.Equal(3, response.Phonemes.Length)
        Assert.NotEqual<string>("æ", response.Phonemes.[1].Ipa)

[<Fact>]
let ``AnalyzeService keeps expected vowel for pat-like production`` () =
    let service = AnalyzeService(acousticOnlyAlignmentOptions (), piperOptions (), contentRoot ())
    let wav = pVtWav 900.0

    match service.Analyze(wav, "pat", "en-US") with
    | Error err -> Assert.Fail($"Expected OK, got {err}")
    | Ok response -> Assert.Equal("æ", response.Phonemes.[1].Ipa)

[<Fact>]
let ``AnalyzeService infers pit from user pit wav fixture when practicing pat`` () =
    let fixture =
        Path.Combine(repoRoot (), "tests", "fixtures", "pit-practice-pat.wav")

    if not (File.Exists fixture) || not (WhisperTranscriber.isRuntimeAvailable ()) then
        () // Real recording fixture or Whisper runtime unavailable
    else
        WhisperTranscriber.resetWorker ()

        try
            let service = AnalyzeService(alignmentOptions (), piperOptions (), contentRoot ())
            let wav = File.ReadAllBytes fixture

            match service.Analyze(wav, "pat", "en-US") with
            | Error err -> Assert.Fail($"Expected OK, got {err}")
            | Ok response ->
                Assert.Equal("ɪ", response.Phonemes.[1].Ipa)
                Assert.Equal(Some "whisper:pit", response.Metadata.PhonemeInference)
        finally
            WhisperTranscriber.resetWorker ()
