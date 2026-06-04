module AnalyzeServiceTests

open System
open VoiceRay.Core
open VoiceRay.Infrastructure
open Xunit

let private alignmentOptions () =
    { Provider = AlignmentProvider.Whisper
      WhisperCacheDir = None
      MfaWorkerUrl = None }

let private wav16kMono () =
    let samples = Array.init 3200 (fun i -> int16 (sin (float i / 50.0) * 4000.0))
    WavTestHelpers.encodeMonoPcm 16000 1 samples

[<Fact>]
let ``AnalyzeService rejects unknown word`` () =
    let service = AnalyzeService(alignmentOptions ())
    let wav = wav16kMono ()

    match service.Analyze(wav, "xyzzy", "en-US") with
    | Error AnalyzeServiceError.G2pUnavailable -> ()
    | other -> Assert.Fail($"Expected G2pUnavailable, got {other}")

[<Fact>]
let ``AnalyzeService returns phonemes keyframes scores and metadata`` () =
    let service = AnalyzeService(alignmentOptions ())
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

    let service = AnalyzeService options
    let wav = wav16kMono ()

    match service.Analyze(wav, "pat", "en-US") with
    | Error err -> Assert.Fail($"Expected OK, got {err}")
    | Ok response -> Assert.Equal("mfa-stub", response.Metadata.AlignmentEngine)
