# VoiceRay execution status

> Living run log for parallel agents. Update sections you own; coordinator reconciles before marking epic complete.

## Run metadata

| Field | Value |
| ----- | ----- |
| Started | 2026-06-03 |
| Epic | [KAN-46](https://tomedev.atlassian.net/browse/KAN-46) — VoiceRay Web MVP (OSS local, Phases 0–4) |
| Integration branch | `feature/voiceray-mvp` (merge all ticket branches here) |
| Release PR | **One PR** from `feature/voiceray-mvp` → `main` at epic completion |
| Base branch | `main` |
| Speech | **Local OSS only** — Piper TTS, MFA/Whisper alignment; **no Azure** initially |
| SDK | .NET 10 (fallback .NET 9 in `global.json` if needed) |
| GPU | CUDA preferred; **CPU fallback must show obvious UI banner** |
| Models | Repo `models/` (gitignored) + reuse `%USERPROFILE%\.cache\whisper\` when Whisper used |

## Demo word set (`en-US`)

pat, pet, pit, pot, put, cat, dog, think, red, ship — see plan for teaching focus.

## Plan todos (`docs/plan.md` frontmatter)

| ID | Jira | Status | Merge ref |
| -- | ---- | ------ | --------- |
| scaffold-solution | KAN-48 | done | `ed14360` |
| vocal-tract-svg | KAN-50 | done | `6418dd8` |
| api-contract | KAN-49 | done | `cbcd6bb` |
| backend-reference | KAN-51 | done | `8c63e3f` |
| backend-analyze | KAN-52 | done | `4ead801` |
| backend-compare | KAN-53 | done | `8aa67f7` |
| frontend-flows | KAN-54 | done | `1018650` (merged with W7) |
| docs-multilingual | KAN-55 | done | `1e34b3c` |

## Phase checklist

| Phase | Status | Notes |
| ----- | ------ | ----- |
| 0 Foundation | done | KAN-48 |
| 1 Reference + SVG | done | KAN-50, KAN-51 |
| 2 Record + replay | done | KAN-52, KAN-54 |
| 3 Compare + coaching | done | KAN-53, KAN-54 ghost overlay |
| 4 Multilingual + MFA | partial | KAN-55 docs + MFA Docker stub; locale packs + PWA deferred |

## WIP policy

**Epic implementation complete on `feature/voiceray-mvp`.** Next: final gates + single PR → `main`.

## Gate evidence (integration)

| Gate | Result |
| ---- | ------ |
| `dotnet build` / `dotnet test` | 32 passed (post-W6); re-run after W7 merge |
| `npm run build` / `npm run test` (client/) | KAN-54: 9 unit + 4 Playwright |
| Playwright | KAN-54 e2e (`client/e2e/flows.spec.js`) |

## Merge order (into `feature/voiceray-mvp`)

All ticket branches merged. Release PR: https://github.com/edwardjcw/VoiceRay/pull/1 → `main`.

## Blockers

None.

## 2026-06-04 — Auto-provision + sagittal fix (`feature/auto-provision-sagittal-fix`)

| Item | Status |
| ---- | ------ |
| Branch | `feature/auto-provision-sagittal-fix` |
| Piper auto-provision | `PiperProvisioner.fs`; retry on reference; `POST /api/v1/provision/speech` |
| Health | `speech` capabilities in `GET /api/v1/health` |
| UI | Setup banner + **Set up speech engine**; auto-download on first use (Windows) |
| Sagittal | `vocal-tract.svg` uses `vocal-tract-reference.png` (217×232); overlays for animation |
| Assets | Restored `assets/vocal-tract/reference.png`; copied to `client/public/` |
| Gates | `dotnet test` 32 pass; `npm run build` + 9 unit + 4 Playwright pass |

## 2026-06-04 — Unified setup log + all resources

| Item | Status |
| ---- | ------ |
| Setup API | `GET /api/v1/setup/status`, `POST /api/v1/setup/run` (background + polled log) |
| Resources | Piper TTS, Whisper cache, sagittal assets (`vocalTract`); MFA listed optional |
| UI | Resource setup panel with per-resource status + scrolling log; status line mirrors latest log line |
| Reference | No longer blocks HTTP on download — setup runs async first, then fast `POST /reference` |
| Fix | Removed duplicate inline Piper provision + health auto-download race (stuck "Loading reference…") |

## 2026-06-06 — Piper stdin hang + activity log during reference

| Item | Status |
| ---- | ------ |
| Root cause | Piper reads text from **stdin**, not CLI args — synthesis hung forever |
| Fix | `PiperTts.fs` writes text to stdin; 120s timeout; logs to `GET /api/v1/setup/status` |
| UI | Activity panel stays visible during reference load; polls backend log every 400ms |

## KAN-54 commit gate

| Item | Status |
| ---- | ------ |
| Commit | `1018650` |
| Post-work npm | 9 unit + 4 Playwright |
| UI | Practice/record/compare, deviceBanner, ghost overlay |

## 2026-06-08 — Sagittal rig rework (parametric articulators) (`feature/sagittal-rig-rework`)

| Item | Status |
| ---- | ------ |
| Branch | `feature/sagittal-rig-rework` |
| Problem | Sagittal view was a static reference image; old per-layer `transform`/`d` poses were inaccurate |
| New contract | `Contract.fs`: removed `LayerPose`; `ArticulatoryKeyframe` now carries `ArticulatoryPose` (normalized `0..1` params: jawOpen, tongueHeight, tongueBackness, tongueTip, interdental, lipRounding, lipClosure, velum) |
| Backend poses | `PoseMap.fs` re-authored: IPA → `ArticulatoryPose` grounded in vowel chart + consonant place/manner; unknown IPA → `neutral` |
| SVG | `client/public/vocal-tract.svg` rebuilt from `assets/vocal-tract/reference.svg` — traced static anatomy (outline/palate/pharynx) + animated tongue (transform on traced path) + procedural tongue_tip/velum/lips |
| Frontend rig | `SagittalPlayer.js` rewritten: parametric geometry generators, `applyPose`, `lerpPose`/`poseAtTime` smooth tweening (coarticulation); light theme bg in `style.css` |
| Docs | `api.md` (ArticulatoryPose schema + example), `articulatory-model.md` (parametric model, layer legend, families) |
| Pre-work gates | `dotnet test` green; `npm run build` clean (after clearing stale Api/vite locks) |
| Post-work gates | `dotnet test` 39 pass; `npm run test` 14 unit + 4 Playwright mock e2e pass; `npm run build` clean |
| UI/API validation | Mock e2e updated (`mockApi.js` emits `pose`); backend test asserts bilabial `LipClosure>0.5`; dev-server smoke load confirms rig mounts neutral pose without console errors |
| Verification | Production `SagittalPlayer` + real SVG rendered all pose families in browser harness; dev server boot confirmed |

### Wav2Vec2 recommendation (Phase 2 follow-up — not in this PR)

Per user question (`runtime: onnx`, `scope: rig_first`): this PR delivers the **rig first**. The phoneme/alignment replacement is a separate follow-up:

- Replace heuristic alignment (`OssAlignment.fs` even-spread + `UserPhonemeInference.fs` whole-word Whisper match + `AcousticVowelProbe.fs` DSP vowel guess) with **Wav2Vec2 phoneme CTC + forced alignment** via **ONNX Runtime** (in-process, no Python).
- Suggested model: `facebook/wav2vec2-lv-60-espeak-cv-ft` (IPA output) exported to ONNX; map CTC frames → phoneme timestamps for both `/analyze` (user) and to tighten `/reference` timing.
- Benefit: real per-phoneme timestamps drive accurate keyframe windows for the new rig and remove the demo-lexicon limitation.

## 2026-06-08 — Wav2Vec2 phoneme recognition + alignment (ONNX) (`feature/wav2vec2-phoneme-onnx`)

Delivers the Phase 2 follow-up: replaces the heuristic recognition/alignment stack with an in-process wav2vec2 espeak phoneme model (no Python at runtime).

| Item | Status |
| ---- | ------ |
| Branch | `feature/wav2vec2-phoneme-onnx` |
| Problem | Alignment was even-spread G2P; user phonemes came from a Whisper whole-word lexicon match + a DSP vowel guess — brittle and lexicon-bound |
| Core algorithms | `VoiceRay.Core/Ctc.fs`: greedy CTC decode (collapse+blank-drop), CTC forced alignment (Viterbi over blank-extended target), frame→ms mapping — all pure + unit tested |
| Vocab | `Wav2Vec2Vocab.fs`: parses `vocab.json`, blank/special detection, espeak→en-US IPA normalization (drops length/stress marks; ɐ→ə, ᵻ→ɪ, …) |
| ONNX inference | `Wav2Vec2Phoneme.fs`: cached `InferenceSession` (Microsoft.ML.OnnxRuntime 1.26), zero-mean/unit-var feature prep, `OrtValue` run, greedy decode → timed `PhonemeSegment`s; graceful `Unavailable` reasons |
| Provisioning | `Wav2Vec2Provisioner.fs`: downloads `model_quantized.onnx` (~318 MB) + `vocab.json` from `onnx-community/wav2vec2-lv-60-espeak-cv-ft-ONNX` (pinned commit) to `models/wav2vec2/` (gitignored) |
| Provider wiring | `AlignmentProvider.Wav2Vec2` (+`Phoneme` alias); `appsettings.json` default `Provider=Wav2Vec2`; `OssAlignment` keeps whisper-stub baseline naming |
| Analyze path | `UserPhonemeInference.infer` prefers wav2vec2 recognition when provisioned, else falls back to the legacy Whisper/acoustic/G2P chain; `AnalyzeService` sets `alignmentEngine=wav2vec2` |
| Setup/health | `wav2vec2` resource in `SetupProvisioner` (optional, auto-provisionable) + `wav2vec2Ready` in `SpeechCapabilities`; warm-up at startup in `Program.fs` |
| Tests | Core: 10 CTC + 4 vocab. Api: fallback test (model absent) + gated real-recognition test (model present). Frontend mock + real-API integration made engine/acoustic-agnostic |
| Verification | Provisioned the real model in-session; backend gated test recognizes the pit fixture acoustically as `[p,e,t]` (front vowel, not the prompted /æ/) via `alignmentEngine=wav2vec2`; 3 real-API Playwright integration tests pass against the live model |
| Post-work gates | `dotnet test` (Core 41 + Api 19) green; `npm run test` (14 unit + 4 mock e2e + 3 integration) green; `dotnet build` / `npm run build` clean |
| Remaining | `/reference` still uses even-spread timing — `Ctc.forcedAlign` is ready to tighten it (next). Full-precision model can replace int8 `model_quantized.onnx` for finer vowel accuracy |

## 2026-06-09 — Reference CTC forced alignment + configurable model variant (`feature/wav2vec2-reference-alignment`)

Tightens `/reference` timing with CTC forced alignment and makes the wav2vec2 precision variant configurable.

| Item | Status |
| ---- | ------ |
| Branch | `feature/wav2vec2-reference-alignment` |
| Task 1 — reference timing | `Wav2Vec2Phoneme.tryForcedAlign repoRoot wav targetIpa`: normalizes the Piper WAV → 16 kHz mono, maps each en-US target IPA back to a model token (reverse of `normalizeIpa`: exact vocab token, else lowest-id token whose `normalizeIpa` matches), runs logits, `Ctc.forcedAlign`, `Ctc.spanToMs`, keeping the original target IPA on the segments |
| Reference wiring | `ReferenceService` now takes `(PiperOptions, AlignmentOptions, repoRoot)`; when `Provider=Wav2Vec2` and the model is ready it forced-aligns the G2P IPA against the Piper audio and builds keyframes from the aligned phonemes; **any** failure (model absent / unmapped symbol / infeasible) transparently falls back to even-spread `ReferencePipeline.buildSession`. DI updated in `Program.fs` (mirrors `AnalyzeService`) |
| Task 2 — model variant | `Wav2Vec2Provisioner` is variant-aware (`model` fp32 / `model_fp16` / `model_quantized` int8). Variant resolves env `VOICERAY_WAV2VEC2_VARIANT` > config `Speech:Alignment:Wav2Vec2:ModelVariant` > built-in default; the chosen variant drives both download filename and load path. **Default raised to `model` (full fp32).** Plumbed through `AlignmentOptions.Wav2Vec2Variant`; `Program.fs` calls `Wav2Vec2Provisioner.setDefaultVariant` at startup |
| fsproj order | Moved `ProvisionLog`/`Wav2Vec2Provisioner` before `AlignmentOptions`, and `ReferenceService` after `Wav2Vec2Phoneme` (F# compile-order) |
| Tests | `ReferenceServiceTests`: gated forced-alignment test (non-even-spread, ordered/in-range) + gated fallback test (Provider=Wav2Vec2, model absent → exact even-spread match). New `PitPetVowelTests`: gated pit/pet analyze + compare checks. All gated on `Wav2Vec2Phoneme.isReady` |
| Verification (pit/pet) | Provisioned **all three variants locally** and ran the fixtures through analyze: |

### Variant comparison on the minimal-pair fixtures (pinned commit `c69750f…`)

| Variant | pit nucleus | pet nucleus | separates pair? | speed (CPU) | notes |
| ------- | ----------- | ----------- | --------------- | ----------- | ----- |
| `model` (fp32) | **/e/** | **/ɛ/** | ✅ (e ≠ ɛ ≠ æ) | ~fast | default |
| `model_quantized` (int8) | /e/ | /ɛ/ | ✅ | ~fast | identical result to fp32 here |
| `model_fp16` | /ɑ/ (degraded) | — | ❌ | **~6–10× slower** | CPU EP up-casts fp16→fp32; both slower and less accurate — **avoid on CPU** |

**Finding (honest):** every usable variant resolves the speaker's lax **/ɪ/ in `pit` as /e/**, not /ɪ/ — i.e. the `/ɪ/→/e/` reading is **acoustic**, not a quantization artifact (it does not change between int8 and fp32). What the precision upgrade *does* guarantee for coaching holds: the two nuclei are **distinct** (`pit` /e/ ≠ `pet` /ɛ/) and **both differ from the prompted /æ/**, yielding `æ→e` (pit) and `æ→ɛ` (pet) substitutions vs the `pat` reference. The default is `model` (fp32) for maximum precision/headroom; `model_fp16` is explicitly **not** recommended on the CPU execution provider. Tests assert exactly what the model produces (`/e/` for pit), per the "acoustically honest" rule. |

| Models | All variants live under `models/wav2vec2/` (gitignored); Piper provisioned under `models/piper/` |
| Docs | `providers.md` (variant table + config + forced-alignment), `api.md` (reference timing note), this entry |

## 2026-06-09 — Typed multilingual words + compare spectrogram (`feature/typed-word-multilingual-spectrogram`)

Moves past the fixed demo-word picker: users **type any word** and pick a **language (en-US / fr-FR)**, and the Compare phase gains a **toggleable side-by-side spectrogram**.

| Item | Status |
| ---- | ------ |
| Branch | `feature/typed-word-multilingual-spectrogram` |
| Approach (typed words) | Per user decision **A**: synthesize the typed word with the locale's Piper voice, then recognize phonemes from that audio with the existing wav2vec2 espeak model. No new model dependency; works for any Piper language |
| Model note | Research confirms `wav2vec2-lv-60-espeak-cv-ft` (fp32 default) is the best general **multilingual** espeak phoneme model available pre-built as ONNX. A French-specific phonemizer would need a Python/torch ONNX export (conflicts with no-Python-at-runtime) → deferred; model source stays configurable |
| Piper voices | `PiperOptions.Voices` (locale→ONNX) + `resolveVoice`/`isVoiceReady`; `PiperProvisioner.tryProvisionLocale` downloads the binary + locale voice on demand (fr-FR `fr_FR-siwis-medium` from rhasspy/piper-voices); `PiperTts.synthesizeWithVoice` |
| Reference | `ReferenceService`: demo word → exact G2P (forced-aligned as before); **any other word → `RecognizeSession`** (normalize Piper WAV → `Wav2Vec2Phoneme.tryRecognize` → phonemes/keyframes/ipaDisplay). New `RecognitionUnavailable` error surfaced as 503 `recognition_not_ready` |
| Analyze | Arbitrary/typed words no longer hard-fail on G2P: empty baseline + wav2vec2 recognition; only `G2pUnavailable` when nothing is produced |
| Poses | `PoseMap` is now locale-agnostic (IPA-keyed inventory) + French additions: `y ø œ o e a`, nasals `ɑ̃ ɛ̃ ɔ̃ œ̃` (velum lowered), uvular `ʁ/ʀ/χ`, palatals `ɲ ɥ j`, `w f v s z` |
| Vocab | `normalizeIpa` no longer folds `ɑ̃→ɑ` so French nasals survive recognition |
| Compare | `CompareService` accepts any locale (diff is language-agnostic; en-US coaching copy unchanged) |
| Frontend input | `App.js` word `<select>` → free-text `<input list=word-suggestions>` + language `<select>` (en-US/fr-FR); per-locale suggestion datalist; `demoWords.js` adds `LOCALES`/`SUGGESTIONS_BY_LOCALE` |
| Spectrogram | New `client/src/audio/spectrogram.js` (radix-2 FFT + Hann STFT + heatmap + Web Audio decode); Compare panel has **Show/Hide spectrogram** toggle revealing side-by-side Reference vs Your-recording canvases (lazy-rendered) |
| Tests | Core: `PoseMapTests` (French poses), `PiperOptionsTests` (voice resolution), vocab nasal-preservation. Api: `CompareServiceTests` fr-FR accepted, `ReferenceServiceTests` gated arbitrary-word recognition + gated model-absent path. Frontend: `spectrogram.test.js` (FFT/STFT), e2e language-switch + spectrogram-toggle |
| Gates | `dotnet build` 0/0; `dotnet test` Core 47 + Api 26 pass; `npm run build` clean; `npm run test` 18 unit + 6 mock e2e + 3 real-API integration pass |
| Fix (media path) | Newly synthesized reference audio 404'd on playback: `MediaRoot` was anchored at the repo root (`PiperOptions.load repoRoot`) but static files are served from the API web root (`ContentRoot/wwwroot`). Demo words only worked because their wavs were pre-seeded in the served folder. `Program.fs` now re-anchors `MediaRoot` at `ContentRootPath` so saved `/media/reference/*.wav` is reachable for any word/locale |

## Agent notes

- Final PR per user policy; no per-ticket PRs.
- Phase 4 follow-ups (optional): locale packs, PWA manifest, live MFA HTTP client.
- Phase 2 follow-up: Wav2Vec2 phoneme alignment via ONNX — **delivered** (see 2026-06-08 Wav2Vec2 entry).
- `/reference` acoustically-tightened via `Ctc.forcedAlign` — **delivered** (see 2026-06-09 entry).
- `model_fp16` is slower + less accurate on the CPU EP; prefer `model` (fp32) or `model_quantized` (int8).
