module PoseMapTests

open VoiceRay.Core
open Xunit

[<Fact>]
let ``tryGetPose resolves French front rounded vowel /y/`` () =
    match PoseMap.tryGetPose "fr-FR" "y" with
    | None -> Assert.Fail("expected a pose for /y/")
    | Some entry ->
        Assert.True(entry.Pose.LipRounding > 0.5, "front rounded /y/ should be rounded")
        Assert.True(entry.Pose.TongueHeight > 0.7, "/y/ is a high vowel")

[<Fact>]
let ``tryGetPose marks French nasal vowels with a lowered velum`` () =
    for ipa in [ "ɑ̃"; "ɛ̃"; "ɔ̃"; "œ̃" ] do
        match PoseMap.tryGetPose "fr-FR" ipa with
        | None -> Assert.Fail($"expected a pose for nasal vowel /{ipa}/")
        | Some entry -> Assert.True(entry.Pose.Velum > 0.5, $"nasal vowel /{ipa}/ should lower the velum")

[<Fact>]
let ``tryGetPose is locale-agnostic for shared IPA`` () =
    // The inventory is keyed by IPA, so the same symbol resolves regardless of locale tag.
    let enPose = PoseMap.tryGetPose "en-US" "t"
    let frPose = PoseMap.tryGetPose "fr-FR" "t"
    Assert.True(enPose.IsSome)
    Assert.Equal(enPose, frPose)

[<Fact>]
let ``tryGetPose resolves the French uvular rhotic`` () =
    match PoseMap.tryGetPose "fr-FR" "ʁ" with
    | None -> Assert.Fail("expected a pose for uvular /ʁ/")
    | Some entry -> Assert.True(entry.Pose.TongueBackness > 0.7, "uvular /ʁ/ is articulated far back")

[<Fact>]
let ``keyframesForTimeline emits one keyframe per French phoneme`` () =
    let phonemes =
        [ { Ipa = "ʃ"; StartMs = 0; EndMs = 100 }
          { Ipa = "a"; StartMs = 100; EndMs = 200 } ]

    let keyframes = PoseMap.keyframesForTimeline "fr-FR" phonemes
    Assert.Equal(2, keyframes.Length)
