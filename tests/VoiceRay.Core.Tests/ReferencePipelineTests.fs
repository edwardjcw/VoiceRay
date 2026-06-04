module ReferencePipelineTests

open VoiceRay.Core
open Xunit

[<Theory>]
[<InlineData("pat", "pæt", 3)>]
[<InlineData("pet", "pɛt", 3)>]
[<InlineData("ship", "ʃɪp", 3)>]
[<InlineData("think", "θɪŋ", 3)>]
let ``G2P stub resolves demo words`` (word, expectedDisplay, phonemeCount) =
    match G2pStub.tryLookup "en-US" word with
    | None -> Assert.Fail $"Expected G2P for {word}"
    | Some g2p ->
        Assert.Equal(expectedDisplay, g2p.IpaDisplay)
        Assert.Equal(phonemeCount, g2p.IpaSymbols.Length)

[<Fact>]
let ``Reference pipeline builds keyframes for pat`` () =
    match G2pStub.tryLookup "en-US" "pat" with
    | None -> Assert.Fail "G2P missing for pat"
    | Some g2p ->
        let session = ReferencePipeline.buildSession "en-US" g2p 320
        Assert.Equal(3, session.Phonemes.Length)
        Assert.True(session.Keyframes.Length >= 2)

        let first = session.Keyframes.[0]
        Assert.Equal("p", first.Ipa)
        Assert.True(first.Layers.ContainsKey "lips_upper")
        Assert.Contains("bilabial", first.Highlight)

[<Fact>]
let ``Phoneme timeline spans full duration`` () =
    let segments = G2pStub.buildTimeline [ "p"; "æ"; "t" ] 300
    Assert.Equal(3, segments.Length)
    Assert.Equal(0, segments.[0].StartMs)
    Assert.Equal(300, segments.[2].EndMs)
