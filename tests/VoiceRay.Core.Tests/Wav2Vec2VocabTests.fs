module Wav2Vec2VocabTests

open VoiceRay.Core
open VoiceRay.Infrastructure
open Xunit

let private sampleJson =
    """{"<pad>":0,"<s>":1,"</s>":2,"<unk>":3,"æ":4,"ɑː":5,"iː":6,"ᵻ":7,"t":8,"ɐ":9}"""

[<Fact>]
let ``tryParse reads tokens and identifies the blank/specials`` () =
    match Wav2Vec2Vocab.tryParse sampleJson with
    | None -> Assert.Fail("expected vocab to parse")
    | Some vocab ->
        Assert.Equal(0, vocab.BlankId)
        Assert.True(vocab.SpecialIds.Contains 0) // <pad>
        Assert.True(vocab.SpecialIds.Contains 1) // <s>
        Assert.True(vocab.SpecialIds.Contains 3) // <unk>
        Assert.False(vocab.SpecialIds.Contains 4) // æ is a real phoneme
        Assert.Equal("æ", vocab.IdToToken.[4])

[<Fact>]
let ``normalizeIpa folds espeak variants onto the en-US inventory`` () =
    Assert.Equal("ɑ", Wav2Vec2Vocab.normalizeIpa "ɑː") // drop length mark
    Assert.Equal("i", Wav2Vec2Vocab.normalizeIpa "iː")
    Assert.Equal("ɪ", Wav2Vec2Vocab.normalizeIpa "ᵻ") // barred-i → ɪ
    Assert.Equal("ə", Wav2Vec2Vocab.normalizeIpa "ɐ") // near-open central → schwa
    Assert.Equal("æ", Wav2Vec2Vocab.normalizeIpa "æ") // unchanged

[<Fact>]
let ``spanToIpa returns normalized phoneme and skips specials`` () =
    match Wav2Vec2Vocab.tryParse sampleJson with
    | None -> Assert.Fail("expected vocab to parse")
    | Some vocab ->
        let span id =
            { Ctc.TokenId = id; Ctc.StartFrame = 0; Ctc.EndFrame = 1 }

        Assert.Equal(Some "ɑ", Wav2Vec2Vocab.spanToIpa vocab (span 5)) // ɑː → ɑ
        Assert.Equal(Some "æ", Wav2Vec2Vocab.spanToIpa vocab (span 4))
        Assert.Equal(None, Wav2Vec2Vocab.spanToIpa vocab (span 1)) // <s> special filtered

[<Fact>]
let ``tryParse rejects malformed json`` () =
    Assert.True((Wav2Vec2Vocab.tryParse "not json").IsNone)
