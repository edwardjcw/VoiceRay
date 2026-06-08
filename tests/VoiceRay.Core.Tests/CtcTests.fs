module CtcTests

open VoiceRay.Core
open Xunit

/// Builds a logit row of `size` with a strong peak at `peak` (others near zero).
let private row (size: int) (peak: int) : float32[] =
    Array.init size (fun i -> if i = peak then 6.0f else 0.0f)

[<Fact>]
let ``greedyDecode drops blanks and collapses repeats`` () =
    // argmax frames: blank, a, a, blank, a, b   (blank=0, a=1, b=2)
    let logits = [| row 3 0; row 3 1; row 3 1; row 3 0; row 3 1; row 3 2 |]
    let spans = Ctc.greedyDecode 0 logits
    let ids = spans |> List.map (fun s -> s.TokenId)
    Assert.Equal<int list>([ 1; 1; 2 ], ids) // repeat split by blank => two separate /a/

[<Fact>]
let ``greedyDecode records contiguous frame spans`` () =
    let logits = [| row 3 1; row 3 1; row 3 0; row 3 2 |]
    let spans = Ctc.greedyDecode 0 logits

    match spans with
    | [ a; b ] ->
        Assert.Equal(1, a.TokenId)
        Assert.Equal(0, a.StartFrame)
        Assert.Equal(2, a.EndFrame) // frames 0..1, exclusive end
        Assert.Equal(2, b.TokenId)
        Assert.Equal(3, b.StartFrame)
        Assert.Equal(4, b.EndFrame)
    | other -> Assert.Fail($"expected two spans, got {other.Length}")

[<Fact>]
let ``greedyDecode on empty input returns empty`` () =
    Assert.Empty(Ctc.greedyDecode 0 [||])

[<Fact>]
let ``greedyDecode of all-blank frames returns empty`` () =
    let logits = [| row 3 0; row 3 0; row 3 0 |]
    Assert.Empty(Ctc.greedyDecode 0 logits)

[<Fact>]
let ``forcedAlign returns one ordered span per target token`` () =
    // 5 frames, target tokens [1; 2]; evidence: frames 0-2 favor 1, frames 3-4 favor 2.
    let logits = [| row 3 1; row 3 1; row 3 1; row 3 2; row 3 2 |]

    match Ctc.forcedAlign 0 [ 1; 2 ] logits with
    | None -> Assert.Fail("expected a feasible alignment")
    | Some spans ->
        Assert.Equal(2, spans.Length)
        Assert.Equal(1, spans.[0].TokenId)
        Assert.Equal(2, spans.[1].TokenId)
        // monotonic, non-overlapping, every token gets at least one frame
        Assert.True(spans.[0].EndFrame <= spans.[1].StartFrame)
        Assert.True(spans.[0].EndFrame > spans.[0].StartFrame)
        Assert.True(spans.[1].EndFrame > spans.[1].StartFrame)
        // token 1 should win the early frames, token 2 the later ones
        Assert.True(spans.[0].StartFrame = 0)
        Assert.True(spans.[1].EndFrame = 5)

[<Fact>]
let ``forcedAlign handles adjacent identical tokens`` () =
    // target [1; 1] needs a blank between the two identical tokens.
    let logits = [| row 3 1; row 3 0; row 3 1 |]

    match Ctc.forcedAlign 0 [ 1; 1 ] logits with
    | None -> Assert.Fail("expected feasible alignment for repeated token")
    | Some spans ->
        Assert.Equal(2, spans.Length)
        Assert.True(spans.[0].EndFrame <= spans.[1].StartFrame)

[<Fact>]
let ``forcedAlign is infeasible when tokens exceed frames`` () =
    let logits = [| row 3 1 |]
    Assert.True((Ctc.forcedAlign 0 [ 1; 2; 1 ] logits).IsNone)

[<Fact>]
let ``forcedAlign of empty target returns empty`` () =
    let logits = [| row 3 1 |]
    Assert.Equal<Ctc.TokenSpan list option>(Some [], Ctc.forcedAlign 0 [] logits)

[<Fact>]
let ``spanToMs scales frames by audio duration`` () =
    // 10 frames over 200 ms => 20 ms/frame
    let span = { Ctc.TokenId = 1; Ctc.StartFrame = 2; Ctc.EndFrame = 5 }
    let startMs, endMs = Ctc.spanToMs 10 200 span
    Assert.Equal(40, startMs)
    Assert.Equal(100, endMs)

[<Fact>]
let ``spanToMs clamps the final frame to the duration`` () =
    let span = { Ctc.TokenId = 1; Ctc.StartFrame = 9; Ctc.EndFrame = 10 }
    let _, endMs = Ctc.spanToMs 10 200 span
    Assert.Equal(200, endMs)
