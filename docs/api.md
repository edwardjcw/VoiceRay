# VoiceRay HTTP API (v1)

Base path: `/api/v1`. JSON bodies use **camelCase** property names unless noted.

OpenAPI document (development): `GET /openapi/v1.json` when the API runs with OpenAPI enabled.

Implementation types live in `VoiceRay.Core` (`Contract.fs`). `POST /api/v1/reference` is implemented (W4, Piper + G2P stub + keyframes). `POST /api/v1/analyze` is implemented (W5, WAV normalize + OSS Whisper/MFA alignment stubs). `POST /api/v1/compare` is implemented (W6, greedy IPA alignment + en-US coaching rules).

---

## Health

### `GET /api/v1/health`

**Response** `200 application/json`

```json
{
  "status": "ok",
  "product": "VoiceRay",
  "apiVersion": "v1",
  "speechProvider": "Local",
  "speech": {
    "piperReady": true,
    "piperStatus": "ready",
    "canAutoProvision": true,
    "whisperCacheAvailable": false,
    "wav2vec2Ready": true,
    "vocalTractReady": true,
    "allRequiredReady": true,
    "setupState": "succeeded"
  }
}
```

`wav2vec2Ready` indicates the in-process phoneme model (ONNX) is provisioned; when true, `/analyze` uses it for recognition + alignment.

### `GET /api/v1/setup/status`

Returns setup run state, rolling log lines, and per-resource status (`piper`, `wav2vec2`, `whisper`, `vocalTract`, `mfa`).

### `POST /api/v1/setup/run`

Starts background setup for all auto-provisionable resources. Poll `GET /api/v1/setup/status` for progress.

**Response** `202` when started; `200` when already ready; `409` when a run is already in progress.

### `POST /api/v1/provision/speech`

Legacy alias for `POST /api/v1/setup/run`.

---

---

## Reference session

Generates reference TTS audio, IPA phoneme timeline, and articulatory keyframes for practice mode.

### `POST /api/v1/reference`

**Request** `application/json`

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| `text` | string | yes | Word or phrase to synthesize |
| `locale` | string | yes | BCP-47 tag (e.g. `en-US`) |

```json
{
  "text": "pat",
  "locale": "en-US"
}
```

**Response** `200 application/json`

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| `audioUrl` | string | no* | URL to fetch reference audio (preferred when hosting static/stream) |
| `audioBase64` | string | no* | Base64-encoded WAV/MP3 when no URL is available |
| `phonemes` | `PhonemeSegment[]` | yes | Timed IPA segments |
| `keyframes` | `ArticulatoryKeyframe[]` | yes | SVG animation keyframes aligned to timeline |
| `ipaDisplay` | string | yes | Human-readable IPA string for UI strip |

\* Provide **at least one** of `audioUrl` or `audioBase64`.

**Phoneme timing:** when the `Wav2Vec2` alignment provider is selected (default) and the model is
provisioned, the known G2P IPA sequence is **CTC forced-aligned** against the synthesized Piper
audio, so `phonemes`/`keyframes` carry real acoustic per-phoneme timestamps (boundaries are not
evenly spread and may not cover the whole clip). If the model is absent, an unmapped symbol is
encountered, or alignment is infeasible, the endpoint transparently falls back to the heuristic
even-spread G2P timeline — the response shape is identical either way.

```json
{
  "audioUrl": "/media/reference/abc123.wav",
  "phonemes": [
    { "ipa": "p", "startMs": 0, "endMs": 80 },
    { "ipa": "æ", "startMs": 80, "endMs": 220 },
    { "ipa": "t", "startMs": 220, "endMs": 320 }
  ],
  "keyframes": [
    {
      "ipa": "p",
      "startMs": 0,
      "endMs": 80,
      "pose": {
        "jawOpen": 0.1,
        "tongueHeight": 0.4,
        "tongueBackness": 0.45,
        "tongueTip": 0,
        "interdental": 0,
        "lipRounding": 0,
        "lipClosure": 1,
        "velum": 0
      },
      "highlight": ["bilabial"]
    }
  ],
  "ipaDisplay": "pæt"
}
```

**Errors**

| Status | When |
| ------ | ---- |
| `400` | Missing/invalid `text` or `locale`; word not in demo lexicon (`en-US`) |
| `503` | Speech engine not ready or synthesis failed (`code`: `speech_not_ready`) |

---

## Analyze recording

Aligns user WAV to reference text; returns user phoneme timeline, keyframes, and scores.

### `POST /api/v1/analyze`

**Request** `multipart/form-data`

| Part | Type | Required | Description |
| ---- | ---- | -------- | ----------- |
| `audio` | file | yes | **16 kHz mono WAV** |
| `text` | string | yes | Same text the user attempted |
| `locale` | string | yes | BCP-47 tag |

**Response** `200 application/json`

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| `phonemes` | `PhonemeSegment[]` | yes | User-aligned IPA timeline |
| `keyframes` | `ArticulatoryKeyframe[]` | yes | User articulatory poses |
| `scores` | `PhonemeScore[]` | yes | Per-phoneme assessment |
| `audioEcho` | string | no | Base64 echo of normalized 16 kHz mono WAV for replay |
| `metadata` | `AnalyzeMetadata` | yes | Alignment engine + compute device banner for UI |

`AnalyzeMetadata`:

| Field | Type | Description |
| ----- | ---- | ----------- |
| `alignmentEngine` | string | `wav2vec2`, `whisper-stub`, or `mfa-stub` (recognition/alignment path used) |
| `computeDevice` | string | `cpu` or `cuda` |
| `deviceBanner` | string | Human-readable banner (CPU fallback vs CUDA) |
| `sampleRateHz` | number | Always `16000` after normalize |
| `channels` | number | Always `1` after normalize |
| `phonemeInference` | string? | Source label: `wav2vec2`, `whisper:<word>`, `acoustic-vowel`, or `text-g2p` |
| `inferredWord` | string? | Demo word the Whisper fallback heard (only on the `whisper:*` path) |
| `inferenceNote` | string? | Human-readable note (e.g. `wav2vec2 heard: pɪt`, or fallback reason) |

When the `Wav2Vec2` model is provisioned (default provider), `/analyze` runs in-process CTC
phoneme recognition + alignment and sets `alignmentEngine: "wav2vec2"` /
`phonemeInference: "wav2vec2"`. If the model is absent, it transparently falls back to the
Whisper word-match / acoustic-vowel / G2P chain.

```json
{
  "phonemes": [{ "ipa": "p", "startMs": 10, "endMs": 90 }],
  "keyframes": [],
  "scores": [{ "ipa": "p", "score": 92.5, "accuracy": "good" }],
  "audioEcho": null,
  "metadata": {
    "alignmentEngine": "wav2vec2",
    "computeDevice": "cpu",
    "deviceBanner": "Alignment running on CPU — enable CUDA for GPU acceleration (VOICERAY_FORCE_CPU unset).",
    "sampleRateHz": 16000,
    "channels": 1,
    "phonemeInference": "wav2vec2",
    "inferenceNote": "wav2vec2 heard: pɪt"
  }
}
```

**Errors**

| Status | When |
| ------ | ---- |
| `400` | Missing audio, wrong format, unknown demo word, or invalid fields |
| `503` | Assessment worker unavailable (reserved) |

---

## Compare timelines

Diffs reference vs user phoneme sequences and returns alignment segments plus coaching messages.

### `POST /api/v1/compare`

**Request** `application/json`

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| `referencePhonemes` | `PhonemeSegment[]` | yes | From `/reference` |
| `userPhonemes` | `PhonemeSegment[]` | yes | From `/analyze` |
| `locale` | string | yes | BCP-47 tag |

```json
{
  "referencePhonemes": [{ "ipa": "t", "startMs": 0, "endMs": 100 }],
  "userPhonemes": [{ "ipa": "d", "startMs": 0, "endMs": 110 }],
  "locale": "en-US"
}
```

**Response** `200 application/json`

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| `segments` | `CompareSegment[]` | yes | Greedy/DTW alignment result |
| `coaching` | `CoachingMessage[]` | yes | Rule-based hints for UI |

```json
{
  "segments": [
    { "kind": "substitution", "referenceIpa": "t", "userIpa": "d" }
  ],
  "coaching": [
    {
      "message": "Use a voiceless alveolar stop, not a voiced one.",
      "highlightLayers": ["tongue", "teeth_upper"],
      "referenceIpa": "t",
      "userIpa": "d"
    }
  ]
}
```

**Errors**

| Status | When |
| ------ | ---- |
| `400` | Empty phoneme lists, missing locale, or unsupported locale (MVP: `en-US` only) |

---

## Shared types

### `PhonemeSegment`

| Field | Type | Description |
| ----- | ---- | ----------- |
| `ipa` | string | IPA symbol for segment |
| `startMs` | int | Inclusive start (ms) |
| `endMs` | int | Exclusive end (ms) |

### `ArticulatoryPose`

Normalized articulator parameters (each `0..1`). The backend emits phonetics; the
frontend `SagittalPlayer` converts these into sagittal SVG geometry (decoupling
phonetics from rig geometry). See [`articulatory-model.md`](articulatory-model.md).

| Field | Type | Description |
| ----- | ---- | ----------- |
| `jawOpen` | number | Mandible aperture: 0 closed → 1 open |
| `tongueHeight` | number | Tongue body height: 0 low → 1 high (≈ inverse F1) |
| `tongueBackness` | number | Tongue advancement: 0 front → 1 back (≈ inverse F2) |
| `tongueTip` | number | Tip raise toward alveolar ridge (coronals): 0 → 1 |
| `interdental` | number | Tip protrusion between teeth (θ/ð): 0 → 1 |
| `lipRounding` | number | Lip rounding/protrusion: 0 spread → 1 rounded |
| `lipClosure` | number | Lip closure (bilabials): 0 open → 1 sealed |
| `velum` | number | Soft palate: 0 raised/oral → 1 lowered/nasal |

### `ArticulatoryKeyframe`

| Field | Type | Description |
| ----- | ---- | ----------- |
| `ipa` | string | Segment IPA |
| `startMs` | int | Window start |
| `endMs` | int | Window end |
| `pose` | `ArticulatoryPose` | Articulator parameters for the segment |
| `highlight` | string[] | UI landmark ids (optional pedagogy) |

### `PhonemeScore`

| Field | Type | Description |
| ----- | ---- | ----------- |
| `ipa` | string | Assessed segment |
| `score` | number | 0–100 style score |
| `accuracy` | string? | Provider label (`good`, `fair`, …) |

### `CompareSegment` (tagged union)

| `kind` | Extra fields |
| ------ | ------------ |
| `match` | — |
| `substitution` | `referenceIpa`, `userIpa` |
| `omission` | `referenceIpa` |
| `insertion` | `userIpa` |

F# representation: `CompareSegment` discriminated union in `VoiceRay.Core.Contract`.

### `CoachingMessage`

| Field | Type | Description |
| ----- | ---- | ----------- |
| `message` | string | Coaching copy |
| `highlightLayers` | string[] | SVG layers to emphasize |
| `referenceIpa` | string? | Related reference segment |
| `userIpa` | string? | Related user segment |

---

## CORS and auth

- CORS: permissive in development (see `Program.fs`).
- Auth: none for local MVP; production may add API keys server-side only (no secrets in the Vite client).
