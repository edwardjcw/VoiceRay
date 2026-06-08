namespace VoiceRay.Core

/// Pure CTC decoding and forced alignment over phoneme logits (e.g. wav2vec2).
///
/// All math is frame-level; conversion to milliseconds uses the audio duration so
/// the result is independent of the model's exact convolutional frame stride.
module Ctc =

    /// A decoded or aligned token with its frame span: `[StartFrame, EndFrame)`.
    type TokenSpan =
        { TokenId: int
          StartFrame: int
          EndFrame: int }

    /// Index of the maximum value in a logit row (ties resolved to the lowest index).
    let argmax (row: float32[]) =
        if isNull row || row.Length = 0 then
            -1
        else
            let mutable bestIdx = 0
            let mutable bestVal = row.[0]

            for i in 1 .. row.Length - 1 do
                if row.[i] > bestVal then
                    bestVal <- row.[i]
                    bestIdx <- i

            bestIdx

    /// Greedy CTC decode: per-frame argmax, collapse consecutive repeats, drop blanks.
    /// Returns one span per emitted (non-blank) token in time order.
    let greedyDecode (blankId: int) (logits: float32[][]) : TokenSpan list =
        let n = if isNull logits then 0 else logits.Length

        if n = 0 then
            []
        else
            let result = ResizeArray<TokenSpan>()
            let mutable prev = blankId
            let mutable curStart = 0
            let mutable hasCur = false

            for t in 0 .. n - 1 do
                let id = argmax logits.[t]

                if id = blankId then
                    if hasCur then
                        result.Add
                            { TokenId = prev
                              StartFrame = curStart
                              EndFrame = t }

                        hasCur <- false

                    prev <- blankId
                elif id = prev && hasCur then
                    () // same token continues — extend the current span
                else
                    if hasCur then
                        result.Add
                            { TokenId = prev
                              StartFrame = curStart
                              EndFrame = t }

                    curStart <- t
                    hasCur <- true
                    prev <- id

            if hasCur then
                result.Add
                    { TokenId = prev
                      StartFrame = curStart
                      EndFrame = n }

            List.ofSeq result

    /// Numerically stable log-softmax of a single logit row.
    let private logSoftmax (row: float32[]) : float[] =
        let m =
            row
            |> Array.fold (fun acc x -> max acc (float x)) System.Double.NegativeInfinity

        let exps = row |> Array.map (fun x -> exp (float x - m))
        let logSum = log (Array.sum exps) + m
        row |> Array.map (fun x -> float x - logSum)

    /// CTC forced alignment of a known target token sequence to logits (Viterbi over the
    /// blank-extended sequence). Returns one span per target token, in order, or `None`
    /// when alignment is infeasible (e.g. fewer frames than required tokens).
    let forcedAlign (blankId: int) (tokens: int list) (logits: float32[][]) : TokenSpan list option =
        let tokenArr = List.toArray tokens
        let s = tokenArr.Length
        let tFrames = if isNull logits then 0 else logits.Length

        if s = 0 then
            Some []
        elif tFrames = 0 then
            None
        else
            // Blank-extended target: [blank, k0, blank, k1, ..., kS-1, blank].
            let lenExt = 2 * s + 1
            let ext = Array.init lenExt (fun i -> if i % 2 = 0 then blankId else tokenArr.[i / 2])
            let logp = logits |> Array.map logSoftmax
            let neginf = System.Double.NegativeInfinity
            let score = Array2D.create tFrames lenExt neginf
            let back = Array2D.create tFrames lenExt -1

            score.[0, 0] <- logp.[0].[ext.[0]]

            if lenExt > 1 then
                score.[0, 1] <- logp.[0].[ext.[1]]

            for t in 1 .. tFrames - 1 do
                for j in 0 .. lenExt - 1 do
                    let mutable bestPrev = j
                    let mutable bestScore = score.[t - 1, j]

                    if j - 1 >= 0 && score.[t - 1, j - 1] > bestScore then
                        bestScore <- score.[t - 1, j - 1]
                        bestPrev <- j - 1

                    if
                        j - 2 >= 0
                        && ext.[j] <> blankId
                        && ext.[j] <> ext.[j - 2]
                        && score.[t - 1, j - 2] > bestScore
                    then
                        bestScore <- score.[t - 1, j - 2]
                        bestPrev <- j - 2

                    if bestScore > neginf then
                        score.[t, j] <- bestScore + logp.[t].[ext.[j]]
                        back.[t, j] <- bestPrev

            let endJ =
                [ lenExt - 1; lenExt - 2 ]
                |> List.filter (fun j -> j >= 0 && score.[tFrames - 1, j] > neginf)
                |> function
                    | [] -> -1
                    | cands -> cands |> List.maxBy (fun j -> score.[tFrames - 1, j])

            if endJ < 0 then
                None
            else
                let path = Array.zeroCreate tFrames
                let mutable j = endJ

                for t in tFrames - 1 .. -1 .. 0 do
                    path.[t] <- j

                    if t > 0 then
                        let p = back.[t, j]
                        if p >= 0 then j <- p

                [ for k in 0 .. s - 1 do
                      let extPos = 2 * k + 1
                      let frames = [ for t in 0 .. tFrames - 1 do if path.[t] = extPos then yield t ]

                      match frames with
                      | [] ->
                          { TokenId = tokenArr.[k]
                            StartFrame = 0
                            EndFrame = 0 }
                      | _ ->
                          { TokenId = tokenArr.[k]
                            StartFrame = List.min frames
                            EndFrame = List.max frames + 1 } ]
                |> Some

    /// Converts a frame span to a `[startMs, endMs)` window using the audio duration.
    let spanToMs (totalFrames: int) (durationMs: int) (span: TokenSpan) =
        if totalFrames <= 0 then
            (0, durationMs)
        else
            let msPerFrame = float durationMs / float totalFrames
            let startMs = max 0 (int (float span.StartFrame * msPerFrame))
            let endMs = int (float span.EndFrame * msPerFrame)
            (startMs, min durationMs (max (startMs + 1) endMs))
