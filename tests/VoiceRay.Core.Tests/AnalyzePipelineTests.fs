module AnalyzePipelineTests

open VoiceRay.Core
open Xunit

[<Fact>]
let ``Analyze pipeline builds scores and keyframes for pat`` () =
    match G2pStub.tryLookup "en-US" "pat" with
    | None -> Assert.Fail "G2P missing"
    | Some g2p ->
        let session = AnalyzePipeline.buildSession "en-US" g2p 400
        Assert.Equal(3, session.Phonemes.Length)
        Assert.Equal(3, session.Scores.Length)
        Assert.True(session.Keyframes.Length >= 2)
        Assert.True(session.Scores.[0].Score >= 55.0)
