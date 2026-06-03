module ReferenceServiceTests

open System
open System.IO
open VoiceRay.Core
open VoiceRay.Infrastructure
open Xunit

let private repoRoot =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", "..", ".."))

let private piperOptions () =
    { Executable = Path.Combine(repoRoot, "models", "piper", "bin", "piper", "piper.exe")
      VoiceModel = Path.Combine(repoRoot, "models", "piper", "voices", "en_US-lessac-medium.onnx")
      MediaRoot = Path.Combine(repoRoot, "src", "VoiceRay.Api", "wwwroot", "media", "reference") }

[<Fact>]
let ``ReferenceService rejects unknown demo word`` () =
    let service = ReferenceService(piperOptions ())
    match service.Generate { Text = "xyzzy"; Locale = "en-US" } with
    | Error ReferenceServiceError.G2pUnavailable -> ()
    | other -> Assert.Fail($"Expected G2pUnavailable, got {other}")

[<Fact>]
let ``ReferenceService returns keyframes for pat when Piper configured`` () =
    let options = piperOptions ()

    if not (PiperOptions.isConfigured options) then
        ()
    else
        let service = ReferenceService options

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
        let service = ReferenceService options

        for word in demoWords do
            match service.Generate { Text = word; Locale = "en-US" } with
            | Error err -> Assert.Fail($"Word '{word}' failed: {err}")
            | Ok response ->
                Assert.False(List.isEmpty response.Phonemes)
                Assert.False(List.isEmpty response.Keyframes)
                Assert.False(String.IsNullOrWhiteSpace response.IpaDisplay)
