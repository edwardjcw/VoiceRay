# Speech providers (VoiceRay)

> OSS speech stack for the Web MVP epic. **Default: local only** — `Speech:Provider = Local`. Azure Speech is out of scope until explicitly enabled.

## Quick reference

| Component | Path / location | Epic status |
| --------- | ----------------- | ----------- |
| Piper CLI | `models/piper/bin/piper/piper.exe` | Required for reference TTS |
| Piper en-US voice | `models/piper/voices/en_US-lessac-medium.onnx` | Required |
| Wav2Vec2 phoneme model | `models/wav2vec2/<variant>.onnx` + `vocab.json` | **Default** recognition + alignment (auto-download; variant configurable) |
| Whisper cache | `%USERPROFILE%\.cache\whisper\` | Fallback (reuse existing install) |
| MFA worker | `workers/mfa/` Docker | Phase 4 stub (HTTP not wired in API yet) |
| Azure Speech | — | **Deferred** |

## Local TTS (Piper)

[rhasspy/piper](https://github.com/rhasspy/piper) runs as a CLI from Infrastructure (`PiperTtsService`). Reference audio is written under `Speech:Piper:MediaRoot` and exposed at `/media/reference/{id}.wav`.

### Provision Piper (Windows)

**Preferred:** open the VoiceRay UI — the app downloads Piper automatically on first reference load, or use **Set up speech engine** in the header. The API also exposes `POST /api/v1/provision/speech`.

Manual fallback:

```powershell
.\scripts\provision-piper.ps1
```

Release pinned in script: `2023.11.14-2` (`piper_windows_amd64.zip`). Voice: [rhasspy/piper-voices](https://huggingface.co/rhasspy/piper-voices) — `en_US-lessac-medium`.

### Smoke test

```powershell
$piper = "models\piper\bin\piper\piper.exe"
$voice = "models\piper\voices\en_US-lessac-medium.onnx"
& $piper -m $voice -f out.wav -- "Hello from VoiceRay"
```

### App configuration

`src/VoiceRay.Api/appsettings.json`:

```json
"Speech": {
  "Provider": "Local",
  "Piper": {
    "Executable": "models/piper/bin/piper/piper.exe",
    "VoiceModel": "models/piper/voices/en_US-lessac-medium.onnx",
    "MediaRoot": "wwwroot/media/reference"
  }
}
```

**Phase 4:** add Piper voice paths per locale (see locale matrix in [`architecture.md`](architecture.md)).

## Phoneme recognition + alignment (Wav2Vec2 ONNX) — default

The analyze pipeline's default path runs an in-process wav2vec2 espeak phoneme model via
[ONNX Runtime](https://onnxruntime.ai/) (`Microsoft.ML.OnnxRuntime`) — **no Python at runtime**.
It performs greedy CTC decoding to recognize the phonemes the user actually produced and derives
per-phoneme timestamps from the CTC frame spans, replacing the heuristic even-spread alignment,
the Whisper whole-word lexicon match, and the DSP vowel probe.

The same model also tightens **`/reference`** timing: the known G2P IPA sequence is **CTC
forced-aligned** (`Ctc.forcedAlign`) against the synthesized Piper audio
(`Wav2Vec2Phoneme.tryForcedAlign`), so reference keyframes carry real acoustic boundaries instead
of an even spread. Any failure (model absent, an IPA symbol that cannot be mapped to a model token,
or an infeasible alignment) transparently falls back to the even-spread `ReferencePipeline.buildSession`.

| Piece | Location |
| ----- | -------- |
| CTC decode + forced alignment (pure) | `VoiceRay.Core/Ctc.fs` |
| Vocab parse + espeak→en-US IPA normalization | `VoiceRay.Infrastructure/Wav2Vec2Vocab.fs` |
| ONNX session + recognition | `VoiceRay.Infrastructure/Wav2Vec2Phoneme.fs` |
| Model download (idempotent) | `VoiceRay.Infrastructure/Wav2Vec2Provisioner.fs` |

### Model

- Source: [`onnx-community/wav2vec2-lv-60-espeak-cv-ft-ONNX`](https://huggingface.co/onnx-community/wav2vec2-lv-60-espeak-cv-ft-ONNX) (ONNX export of `facebook/wav2vec2-lv-60-espeak-cv-ft`, IPA output).
- Files: one ONNX **variant** + `vocab.json`, pinned to commit `c69750f5043e5e1f8a71ab95dd3b98338c280c92`.
- Destination: `models/wav2vec2/<variant>.onnx` (gitignored). Override the directory with `VOICERAY_WAV2VEC2_DIR`.
- Input: 16 kHz mono float waveform, zero-mean/unit-variance (HF feature extractor). Frame stride ≈ 20 ms.

#### Model variant (precision / accuracy tradeoff)

| Variant (`ModelVariant`) | File | Size | Notes |
| ------------------------ | ---- | ---- | ----- |
| `model` (**default**) | `onnx/model.onnx` | ~1.2 GB | Full **fp32** — best front-vowel discrimination; highest RAM/CPU |
| `model_fp16` | `onnx/model_fp16.onnx` | ~603 MB | Half-precision weights; near-fp32 accuracy, smaller download (CPU EP up-casts to fp32 at runtime) |
| `model_quantized` | `onnx/model_quantized.onnx` | ~303 MB | int8 dynamic quant — smallest/lightest, but **coarser vowels** (resolved the lax `/ɪ/` in the `pit` fixture as `/e/`) |

The default was raised from int8 (`model_quantized`) to **full fp32 (`model`)** because the int8
model could not reliably separate adjacent front vowels (e.g. `/ɪ/` vs `/ɛ/`). Choose a lighter
variant when disk/RAM is constrained and exact vowel quality matters less.

### Provision

The **Set up speech engine** flow (`POST /api/v1/setup/run`) downloads the model best-effort
(optional resource `wav2vec2`). Readiness shows as `speech.wav2vec2Ready` in `GET /api/v1/health`.
When the model is absent the pipeline transparently falls back to the Whisper/acoustic/G2P chain.

### Configuration

```json
"Alignment": {
  "Provider": "Wav2Vec2",
  "Wav2Vec2": { "ModelVariant": "model" }
}
```

`Provider` accepts `Wav2Vec2` (default, alias `Phoneme`), `Whisper`, or `Mfa`. Analyze metadata reports
`alignmentEngine: "wav2vec2"` and `phonemeInference: "wav2vec2"` when the model path is used.

`Wav2Vec2:ModelVariant` selects the ONNX precision variant (`model` / `model_fp16` /
`model_quantized`; see the table above). The environment variable **`VOICERAY_WAV2VEC2_VARIANT`**
overrides the config value (and decides both which file is downloaded and which file is loaded).

## Forced alignment (Whisper vs MFA) — fallback

Analyze pipeline (`OssAlignment.fs`) selects an engine:

| Engine | Config trigger | MVP behavior |
| ------ | -------------- | ------------ |
| `whisper-stub` | `Alignment:Provider = Whisper` and Whisper cache directory exists | Timeline from G2P weights over audio duration |
| `mfa-stub` | Whisper cache missing, or `Provider = Mfa`, or `WorkerUrl` empty | Same timeline with +5 ms start nudge (metadata distinguishable) |

### Whisper cache

| Variable / setting | Purpose |
| ------------------ | ------- |
| `%USERPROFILE%\.cache\whisper\` | Default cache when `Whisper:CacheDir` is empty |
| `Speech:Alignment:Whisper:CacheDir` | Override path (junction or copy) |
| `WHISPER_CACHE` | Documented alias for ops (optional env) |

Presence check: `AlignmentOptions.whisperCacheAvailable` — any file under the cache directory.

**Note:** MVP does not invoke the Whisper Python API yet; cache presence only **selects** the whisper-stub code path. Full ASR/alignment integration is post-MVP.

### MFA configuration

```json
"Alignment": {
  "Provider": "Whisper",
  "Whisper": { "CacheDir": "" },
  "Mfa": { "WorkerUrl": "" }
}
```

| Setting | Values | Notes |
| ------- | ------ | ----- |
| `Provider` | `Whisper` (default), `Mfa` | Forces MFA path when set to `Mfa` |
| `Mfa:WorkerUrl` | e.g. `http://localhost:8765` | Base URL for Docker worker (Phase 4) |

When `WorkerUrl` is set, Infrastructure should POST align jobs to the worker (not implemented in W5 — stub only). Until then, empty URL keeps **in-process** `mfa-stub`.

## MFA Docker worker (`workers/mfa/`)

Self-hosted [Montreal Forced Aligner](https://montreal-forced-aligner.readthedocs.io/) container for privacy-sensitive deployments.

### Stub layout (KAN-55)

| File | Purpose |
| ---- | ------- |
| `Dockerfile` | Placeholder image; documents intended MFA + conda base |
| `docker-compose.yml` | Single-service compose on port **8765** |
| `README.md` | Build/run, planned HTTP contract |
| `healthcheck.sh` | Container health probe |

### Run (stub)

```powershell
cd workers\mfa
docker compose up --build
curl http://localhost:8765/health
```

Expected stub response: `{"status":"stub","service":"voiceray-mfa"}`.

### Planned align API (Phase 4)

`POST {WorkerUrl}/v1/align`

| Field | Type | Description |
| ----- | ---- | ----------- |
| `locale` | string | BCP-47 tag |
| `text` | string | Orthographic transcript |
| `audioWavBase64` | string | 16 kHz mono WAV |

Response: `{ "phonemes": [ { "ipa", "startMs", "endMs" } ], "engine": "mfa" }`.

Acoustic/dictionary models will mount under `/models` (not committed; download per MFA docs).

## Compute device banner

Analyze metadata includes `computeDevice` and `deviceBanner`:

- `VOICERAY_FORCE_CPU=1` — force CPU messaging (MVP default for predictable banners).
- `CUDA_PATH` / `CUDA_HOME` — GPU detected for whisper-stub metadata when not forced.

See `VoiceRay.Core/ComputeDevice.fs`.

## Azure (deferred)

Do not add Azure Speech SDK calls or keys in the MVP epic. Future hybrid mode:

| Capability | Azure service |
| ---------- | ------------- |
| Reference TTS + visemes | Neural TTS |
| User pronunciation | Pronunciation Assessment (`Phoneme`, IPA) |

Switch via `Speech:Provider = Azure` when scoped; keep F# pipelines unchanged.

## Related docs

- [`architecture.md`](architecture.md) — pipelines and locale matrix
- [`articulatory-model.md`](articulatory-model.md) — demo words and poses
- [`status.md`](status.md) — model inventory and gate evidence
