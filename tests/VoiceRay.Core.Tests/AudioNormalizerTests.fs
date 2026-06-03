module AudioNormalizerTests

open VoiceRay.Infrastructure
open Xunit

[<Fact>]
let ``Normalizer rejects non-WAV bytes`` () =
    match AudioNormalizer.normalize [| 1uy; 2uy; 3uy |] with
    | Error(AudioNormalizer.InvalidWav _) -> ()
    | other -> Assert.Fail($"Expected InvalidWav, got {other}")

[<Fact>]
let ``Normalizer converts stereo 22050 Hz to 16 kHz mono`` () =
    let sampleRate = 22050
    let frames = 220
    let stereo =
        Array.init (frames * 2) (fun i ->
            if i % 2 = 0 then
                int16 8000
            else
                int16 -8000)

    let input = WavTestHelpers.encodeMonoPcm sampleRate 2 stereo

    match AudioNormalizer.normalize input with
    | Error err -> Assert.Fail($"Expected OK, got {err}")
    | Ok normalized ->
        match AudioNormalizer.tryParsePcm normalized with
        | None -> Assert.Fail "Normalized output should parse as WAV"
        | Some pcm ->
            Assert.Equal(16000, pcm.SampleRate)
            Assert.Equal(1, pcm.Channels)
            Assert.True(pcm.Samples.Length > 0)
