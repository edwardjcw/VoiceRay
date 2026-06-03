module VoiceRay.Core.Tests

open VoiceRay.Core.ApplicationInfo
open Xunit

[<Fact>]
let ``Product name is VoiceRay`` () =
    Assert.Equal("VoiceRay", productName)

[<Fact>]
let ``API version is v1`` () =
    Assert.Equal("v1", apiVersion)
