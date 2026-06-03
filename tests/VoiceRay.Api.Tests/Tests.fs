module VoiceRay.Api.Tests

open VoiceRay.Infrastructure.SpeechConfiguration
open Xunit

[<Fact>]
let ``Default speech provider is Local`` () =
    Assert.Equal(Local, parseProvider None)

[<Fact>]
let ``Azure speech provider parses`` () =
    Assert.Equal(Azure, parseProvider (Some "Azure"))
