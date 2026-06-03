---
name: observe-first-debug
description: Apply an observe-before-reasoning debugging workflow to isolate root causes by inspecting real inputs and outputs at each layer boundary before reading implementation code. Use when debugging failing tests, API endpoints, audio processing issues, unexpected frontend behavior, or when hypotheses are multiplying without confirmed observations.
---

# Observe-First Debugging

When invoked, apply the "observe before reasoning" debugging strategy described below.

## Strategy

**Before analyzing why a system behaves incorrectly, verify what it actually receives and what it actually produces at each layer boundary.**

The real failure point is almost always earlier than the symptoms suggest. A single print of actual output is worth more than a dozen lines of code tracing.

## How to Apply

1. **Identify the layers.** Name each processing stage in the pipeline (for example: Mic -> Frontend API Call -> F# Controller -> Core Logic -> External Service). Draw the data flow, even mentally.
2. **Start at the first layer and observe output, not code.** Do not read implementation files first. Write the smallest possible test or statement that reveals what the first layer actually produces for the failing input.
3. **Check the shape before checking logic.** Ask whether the output has the right type, structure, and count. Wrong values are often a later problem; wrong type or missing data usually signals the root cause area.
4. **Move inward only after the outer layer checks out.** If layer N output is correct, move to layer N+1. If layer N output is wrong, stop there; the bug is in layer N or earlier.
5. **Treat unexpected output as the finding.** If the API returns a 400 instead of a JSON response, that is the root-cause signal, not just a symptom.
6. **Instrument instead of theorizing.** Add focused diagnostics (`printfn`, `console.log`, or equivalent), run once, and inspect the output before forming new hypotheses.

## Signs You Are Reasoning Too Early

- You have read 3+ implementation files and still cannot localize the bug.
- You have multiple hypotheses but none confirmed with observed data.
- You are tracing conditionals without knowing the runtime values of conditions.
- The "obviously wrong" behavior is far downstream from where you are currently looking.

## Example

*Symptom*: The SVG tongue animation doesn't move when audio plays.

*Wrong approach*: Read React/JS animation code -> read SVG layer names -> form hypotheses about GSAP transform values dropping out.

*Right approach*:
1. Log `apiResponse.keyframes` in the frontend -> see it is an empty array `[]`.
2. Trace the empty array back to the API response -> `curl` or inspect Network tab -> see `{"keyframes": []}`.
3. Check the backend `VoiceRay.Api` output -> print the return value of `PoseMap` -> see it returns empty because it didn't recognize the input locale.
4. Conclude: The frontend is working properly; the backend is dropping keyframes due to a locale mismatch.

Total frontend UI code read to reach root cause: 0 files. 

## API/Frontend-Specific Playbook

When debugging API or client-server regressions, use this exact mini-flow:

1. Observe the actual HTTP Request Payload (Body, Headers).
2. Observe the actual HTTP Response Payload (Status, Body).
3. Compare with a successful control case (e.g. testing with a valid mock or known word).
4. If testing logic layers:
   - isolate the Core pure function in a unit test before debugging the API layer.
5. Do not move to the next layer's reasoning until the data boundary shape is confirmed correct.
