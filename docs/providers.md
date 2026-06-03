# Speech providers (VoiceRay)

> Stub for W0/W8. **Epic default: local OSS only** — `Speech:Provider = Local`. No Azure keys in this epic.

## Local (default)

| Component | Path | Notes |
| --------- | ---- | ----- |
| Piper CLI | `models/piper/bin/piper/piper.exe` | [rhasspy/piper](https://github.com/rhasspy/piper) release `2023.11.14-2` (`piper_windows_amd64.zip`) |
| Piper en-US voice | `models/piper/voices/en_US-lessac-medium.onnx` (+ `.onnx.json`) | [rhasspy/piper-voices](https://huggingface.co/rhasspy/piper-voices) — Lessac medium |
| Whisper (alignment) | `%USERPROFILE%\.cache\whisper\` | Reuse existing cache; set `WHISPER_CACHE` or junction — see `docs/status.md` |
| MFA worker | `workers/mfa/` (Phase 4) | Docker — KAN-55 |

### Provision Piper (Windows)

```powershell
.\scripts\provision-piper.ps1
```

Example synthesis (after provision):

```powershell
$piper = "models\piper\bin\piper\piper.exe"
$voice = "models\piper\voices\en_US-lessac-medium.onnx"
& $piper -m $voice -f out.wav -- "Hello from VoiceRay"
```

### App configuration (`src/VoiceRay.Api/appsettings.json`)

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

Reference audio is written under `MediaRoot` and served at `/media/reference/{id}.wav` when synthesis succeeds.

## Azure (deferred)

Not used in the VoiceRay Web MVP epic. Do not add SDK calls requiring keys until explicitly scoped.
