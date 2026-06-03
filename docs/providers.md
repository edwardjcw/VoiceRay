# Speech providers (VoiceRay)

> OSS speech stack for the Web MVP epic. **Default: local only** — `Speech:Provider = Local`. Azure Speech is out of scope until explicitly enabled.

## Quick reference

| Component | Path / location | Epic status |
| --------- | ----------------- | ----------- |
| Piper CLI | `models/piper/bin/piper/piper.exe` | Required for reference TTS |
| Piper en-US voice | `models/piper/voices/en_US-lessac-medium.onnx` | Required |
| Whisper cache | `%USERPROFILE%\.cache\whisper\` | Reuse existing install |
| MFA worker | `workers/mfa/` Docker | Phase 4 stub (HTTP not wired in API yet) |
| Azure Speech | — | **Deferred** |

## Local TTS (Piper)

[rhasspy/piper](https://github.com/rhasspy/piper) runs as a CLI from Infrastructure (`PiperTtsService`). Reference audio is written under `Speech:Piper:MediaRoot` and exposed at `/media/reference/{id}.wav`.

### Provision Piper (Windows)

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

## Forced alignment (Whisper vs MFA)

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
