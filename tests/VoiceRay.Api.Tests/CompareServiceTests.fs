module CompareServiceTests

open VoiceRay.Core
open VoiceRay.Infrastructure
open Xunit

let private segment ipa =
    { Ipa = ipa
      StartMs = 0
      EndMs = 100 }

[<Fact>]
let ``CompareService rejects empty reference phonemes`` () =
    let service = CompareService()

    match
        service.Compare
            { ReferencePhonemes = []
              UserPhonemes = [ segment "t" ]
              Locale = "en-US" }
    with
    | Error(CompareServiceError.InvalidRequest message) ->
        Assert.Contains("referencePhonemes", message)
    | other -> Assert.Fail($"Expected InvalidRequest, got {other}")

[<Fact>]
let ``CompareService accepts non-en-US locales (multilingual diff)`` () =
    let service = CompareService()

    let ref = [ segment "ʃ"; { segment "a" with StartMs = 100; EndMs = 200 } ]
    let user = [ segment "s"; { segment "a" with StartMs = 100; EndMs = 200 } ]

    match service.Compare { ReferencePhonemes = ref; UserPhonemes = user; Locale = "fr-FR" } with
    | Error err -> Assert.Fail($"Expected OK for fr-FR, got {err}")
    | Ok response -> Assert.Contains(CompareSegment.Substitution("ʃ", "s"), response.Segments)

[<Fact>]
let ``CompareService returns segments and coaching for pat vs final stop substitution`` () =
    let service = CompareService()

    let ref =
        [ segment "p"
          { segment "æ" with EndMs = 200 }
          { segment "t" with StartMs = 200 } ]

    let user =
        [ segment "p"
          { segment "æ" with EndMs = 200 }
          { segment "d" with StartMs = 200 } ]

    match service.Compare { ReferencePhonemes = ref; UserPhonemes = user; Locale = "en-US" } with
    | Error err -> Assert.Fail($"Expected OK, got {err}")
    | Ok response ->
        Assert.Contains(CompareSegment.Substitution("t", "d"), response.Segments)
        Assert.True(response.Coaching.Length >= 1)
