module CoreContractTests

open VoiceRay.Core
open VoiceRay.Core.ApplicationInfo
open VoiceRay.Core.ContractJson
open Xunit

[<Fact>]
let ``Product name is VoiceRay`` () =
    Assert.Equal("VoiceRay", productName)

[<Fact>]
let ``API version is v1`` () =
    Assert.Equal("v1", apiVersion)

[<Fact>]
let ``PhonemeSegment round-trips JSON`` () =
    let segment = { Ipa = "æ"; StartMs = 80; EndMs = 220 }
    let restored = deserialize<PhonemeSegment> (serialize segment)
    Assert.Equal(segment, restored)

[<Fact>]
let ``CompareSegment Match serializes tagged JSON`` () =
    let json = serialize CompareSegment.Match
    Assert.Equal("""{"kind":"match"}""", json)
    Assert.Equal(CompareSegment.Match, deserialize<CompareSegment> json)

[<Fact>]
let ``CompareSegment Substitution round-trips`` () =
    let segment = CompareSegment.Substitution("t", "d")
    let json = """{"kind":"substitution","referenceIpa":"t","userIpa":"d"}"""
    Assert.Equal(json, serialize segment)
    Assert.Equal(segment, deserialize<CompareSegment> json)
