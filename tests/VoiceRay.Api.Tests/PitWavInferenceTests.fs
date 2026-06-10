module PitWavInferenceTests

open System
open System.IO
open VoiceRay.Core
open VoiceRay.Infrastructure
open Xunit

let private repoRoot () =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

let private whisperCacheDir () = Path.Combine(repoRoot (), "models", "whisper")

let private pitFixturePath () =
    Path.Combine(repoRoot (), "tests", "fixtures", "pit-practice-pat.wav")

let private alignmentOptions () =
    { Provider = AlignmentProvider.Whisper
      WhisperCacheDir = Some(whisperCacheDir ())
      MfaWorkerUrl = None
      Wav2Vec2Variant = Wav2Vec2Provisioner.defaultVariant }

let private piperOptions () =
    let root = repoRoot ()

    { Executable = Path.Combine(root, "models", "piper", "bin", "piper", "piper.exe")
      VoiceModel = Path.Combine(root, "models", "piper", "voices", "en_US-lessac-medium.onnx")
      MediaRoot = Path.Combine(root, "wwwroot", "media", "reference")
      Voices = Map.empty }

let private whisperReady () =
    Directory.Exists(whisperCacheDir ())
    && File.Exists(pitFixturePath ())
    && WhisperTranscriber.isRuntimeAvailable ()

[<Fact>]
let ``pit wav practicing pat infers ih vowel via whisper`` () =
    if not (whisperReady ()) then
        () // Whisper runtime or fixture unavailable in this environment
    else
        WhisperTranscriber.resetWorker ()

        try
            let service =
                AnalyzeService(alignmentOptions (), piperOptions (), repoRoot ())

            let wav = File.ReadAllBytes(pitFixturePath ())

            match service.Analyze(wav, "pat", "en-US") with
            | Error err -> Assert.Fail($"Expected OK, got {err}")
            | Ok response ->
                Assert.Equal(3, response.Phonemes.Length)
                Assert.Equal("ɪ", response.Phonemes.[1].Ipa)
                Assert.Equal(Some "whisper:pit", response.Metadata.PhonemeInference)
                Assert.Equal(Some "pit", response.Metadata.InferredWord)
        finally
            WhisperTranscriber.resetWorker ()

[<Fact>]
let ``pit wav compare against pat reference coaches ae to ih`` () =
    if not (whisperReady ()) then
        ()
    else
        WhisperTranscriber.resetWorker ()

        try
            let analyze =
                AnalyzeService(alignmentOptions (), piperOptions (), repoRoot ())

            let compare = CompareService()
            let wav = File.ReadAllBytes(pitFixturePath ())

            let referencePhonemes =
                match G2pStub.tryLookup "en-US" "pat" with
                | None ->
                    Assert.Fail("G2P unavailable for pat")
                    []
                | Some g2p -> G2pStub.buildTimeline g2p.IpaSymbols 280

            match analyze.Analyze(wav, "pat", "en-US") with
            | Error err -> Assert.Fail($"Analyze failed: {err}")
            | Ok user ->
                match
                    compare.Compare
                        { Locale = "en-US"
                          ReferencePhonemes = referencePhonemes
                          UserPhonemes = user.Phonemes }
                with
                | Error err -> Assert.Fail($"Compare failed: {err}")
                | Ok result ->
                    Assert.Contains(CompareSegment.Substitution("æ", "ɪ"), result.Segments)
                    Assert.NotEmpty(result.Coaching)
                    Assert.Contains("pit", result.Coaching.[0].Message)
        finally
            WhisperTranscriber.resetWorker ()
