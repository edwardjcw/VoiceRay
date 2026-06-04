module PhonemeDiffTests

open VoiceRay.Core
open Xunit

let private segment ipa startMs endMs =
    { Ipa = ipa
      StartMs = startMs
      EndMs = endMs }

[<Fact>]
let ``Greedy align matches identical IPA sequences`` () =
    let ref = [ segment "p" 0 100; segment "æ" 100 200; segment "t" 200 300 ]
    let user = [ segment "p" 0 100; segment "æ" 100 200; segment "t" 200 300 ]

    let segments = PhonemeDiff.alignGreedy ref user

    Assert.Equal<CompareSegment list>(
        [ CompareSegment.Match
          CompareSegment.Match
          CompareSegment.Match ],
        segments)

[<Fact>]
let ``Greedy align reports final substitution for voiceless stop`` () =
    let ref = [ segment "p" 0 100; segment "æ" 100 200; segment "t" 200 300 ]
    let user = [ segment "p" 0 100; segment "æ" 100 200; segment "d" 200 300 ]

    let segments = PhonemeDiff.alignGreedy ref user

    Assert.Equal<CompareSegment list>(
        [ CompareSegment.Match
          CompareSegment.Match
          CompareSegment.Substitution("t", "d") ],
        segments)

[<Fact>]
let ``Greedy align reports omission when user skips a segment`` () =
    let ref = [ segment "p" 0 100; segment "æ" 100 200; segment "t" 200 300 ]
    let user = [ segment "p" 0 100; segment "t" 200 300 ]

    let segments = PhonemeDiff.alignGreedy ref user

    Assert.Equal<CompareSegment list>(
        [ CompareSegment.Match
          CompareSegment.Omission "æ"
          CompareSegment.Match ],
        segments)

[<Fact>]
let ``Greedy align reports insertion when user adds a segment`` () =
    let ref = [ segment "p" 0 100; segment "t" 200 300 ]
    let user = [ segment "p" 0 100; segment "æ" 100 200; segment "t" 200 300 ]

    let segments = PhonemeDiff.alignGreedy ref user

    Assert.Equal<CompareSegment list>(
        [ CompareSegment.Match
          CompareSegment.Insertion "æ"
          CompareSegment.Match ],
        segments)
