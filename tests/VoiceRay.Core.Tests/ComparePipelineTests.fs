module ComparePipelineTests

open VoiceRay.Core
open VoiceRay.Core.ContractJson
open Xunit

let private segment ipa startMs endMs =
    { Ipa = ipa
      StartMs = startMs
      EndMs = endMs }

[<Fact>]
let ``Compare pipeline emits api example coaching for t to d`` () =
    let ref = [ segment "t" 0 100 ]
    let user = [ segment "d" 0 110 ]

    let response = ComparePipeline.compare "en-US" ref user

    Assert.Equal<CompareSegment list>([ CompareSegment.Substitution("t", "d") ], response.Segments)
    Assert.Single(response.Coaching) |> ignore

    let coach = response.Coaching.[0]
    Assert.Equal("Use a voiceless alveolar stop, not a voiced one.", coach.Message)
    Assert.Equal<string list>([ "tongue"; "teeth_upper" ], coach.HighlightLayers)
    Assert.Equal(Some "t", coach.ReferenceIpa)
    Assert.Equal(Some "d", coach.UserIpa)

[<Fact>]
let ``Compare response round-trips JSON with tagged segments`` () =
    let ref = [ segment "p" 0 80; segment "æ" 80 180; segment "t" 180 280 ]
    let user = [ segment "p" 0 80; segment "æ" 80 180; segment "d" 180 280 ]
    let response = ComparePipeline.compare "en-US" ref user
    let json = serialize response
    let restored = deserialize<CompareResponse> json

    Assert.Equal(response.Segments.Length, restored.Segments.Length)
    Assert.Equal(response.Coaching.Length, restored.Coaching.Length)
    Assert.Contains(CompareSegment.Substitution("t", "d"), restored.Segments)

[<Fact>]
let ``Coaching dedupes repeated substitution pairs`` () =
    let ref = [ segment "t" 0 50; segment "t" 50 100 ]
    let user = [ segment "d" 0 50; segment "d" 50 100 ]

    let response = ComparePipeline.compare "en-US" ref user
    Assert.Equal(2, response.Segments.Length)
    Assert.Equal(1, response.Coaching.Length)
