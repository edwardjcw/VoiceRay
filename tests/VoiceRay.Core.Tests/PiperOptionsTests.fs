module PiperOptionsTests

open System.IO
open VoiceRay.Infrastructure
open Xunit

let private options () =
    { Executable = Path.Combine("models", "piper", "bin", "piper", "piper.exe")
      VoiceModel = Path.Combine("models", "piper", "voices", "en_US-lessac-medium.onnx")
      MediaRoot = Path.Combine("wwwroot", "media", "reference")
      Voices =
        Map.ofList
            [ "en-US", Path.Combine("models", "piper", "voices", "en_US-lessac-medium.onnx")
              "fr-FR", Path.Combine("models", "piper", "voices", "fr_FR-siwis-medium.onnx") ] }

[<Fact>]
let ``resolveVoice returns the locale-specific voice when present`` () =
    let opts = options ()
    Assert.EndsWith("fr_FR-siwis-medium.onnx", PiperOptions.resolveVoice opts "fr-FR")
    Assert.EndsWith("en_US-lessac-medium.onnx", PiperOptions.resolveVoice opts "en-US")

[<Fact>]
let ``resolveVoice falls back to the default voice for unknown locales`` () =
    let opts = options ()
    Assert.Equal(opts.VoiceModel, PiperOptions.resolveVoice opts "de-DE")

[<Fact>]
let ``resolveVoice falls back to the default voice for empty locale`` () =
    let opts = options ()
    Assert.Equal(opts.VoiceModel, PiperOptions.resolveVoice opts "")

[<Fact>]
let ``builtInVoiceFiles cover en-US and fr-FR`` () =
    Assert.True(PiperOptions.builtInVoiceFiles.ContainsKey "en-US")
    Assert.True(PiperOptions.builtInVoiceFiles.ContainsKey "fr-FR")
