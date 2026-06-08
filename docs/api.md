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
    "whisperCacheAvailable": false
  }
}
```

### `GET /api/v1/setup/status`

Returns setup run state, rolling log lines, and per-resource status (`piper`, `whisper`, `vocalTract`, `mfa`).

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
      "layers": {
        "lips_upper": { "transform": "translate(0,0)" },
        "lips_lower": { "transform": "translate(0,2)" }
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
| `alignmentEngine` | string | `whisper-stub` or `mfa-stub` (OSS forced-alignment path) |
| `computeDevice` | string | `cpu` or `cuda` |
| `deviceBanner` | string | Human-readable banner (CPU fallback vs CUDA) |
| `sampleRateHz` | number | Always `16000` after normalize |
| `channels` | number | Always `1` after normalize |

```json
{
  "phonemes": [{ "ipa": "p", "startMs": 10, "endMs": 90 }],
  "keyframes": [],
  "scores": [{ "ipa": "p", "score": 92.5, "accuracy": "good" }],
  "audioEcho": null,
  "metadata": {
    "alignmentEngine": "whisper-stub",
    "computeDevice": "cpu",
    "deviceBanner": "Alignment running on CPU — enable CUDA for GPU acceleration (VOICERAY_FORCE_CPU unset).",
    "sampleRateHz": 16000,
    "channels": 1
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

### `LayerPose`

Per SVG layer id (`lips_upper`, `jaw`, `tongue`, …). At least one of `transform` or `d` should be set when animating.

| Field | Type | Description |
| ----- | ---- | ----------- |
| `transform` | string? | SVG/CSS transform |
| `d` | string? | Path `d` attribute for morph |

### `ArticulatoryKeyframe`

| Field | Type | Description |
| ----- | ---- | ----------- |
| `ipa` | string | Segment IPA |
| `startMs` | int | Window start |
| `endMs` | int | Window end |
| `layers` | object | Map of layer id → `LayerPose` |
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
