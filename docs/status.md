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
| Coordinator | parallel-coordinator; max **2** concurrent implementation leaves; **1** `dotnet test` slot |

## User decisions (locked)

- `docs/status.md` is the resume checkpoint for all agents.
- Per-ticket branch → merge into `feature/voiceray-mvp`; no per-ticket PRs.
- All phases (0–4) in scope for this run.
- Reference art committed at `assets/vocal-tract/reference.png`.
- Demo words: see **Demo word set** below.

## Demo word set (`en-US`)

Standard pedagogical set (varied places/manners):

| # | Word | Teaching focus |
| - | ---- | ---------------- |
| 1 | pat | Voiceless bilabial stop + æ |
| 2 | pet | Voicing + ɛ |
| 3 | pit | High front vowel ɪ |
| 4 | pot | Back rounded ɑ |
| 5 | put | High back ʊ |
| 6 | cat | Velar stop k |
| 7 | dog | Voiced velar ɡ |
| 8 | think | Interdental θ |
| 9 | red | Rhotic /ɹ/ |
| 10 | ship | Post-alveolar ʃ |

## Plan todos (`docs/plan.md` frontmatter)

| ID | Jira | Branch | Status | Notes |
| -- | ---- | ------ | ------ | ----- |
| scaffold-solution | KAN-48 | `feature/w1-scaffold` | done | Merged @ `ed14360` |
| vocal-tract-svg | KAN-50 | `feature/w3-vocal-tract-svg` | done | Merged @ `6418dd8` |
| api-contract | KAN-49 | `feature/w2-api-contract` | done | Merged @ `cbcd6bb` |
| backend-reference | KAN-51 | `feature/w4-reference-pipeline` | pending | Piper from W0 |
| backend-analyze | KAN-52 | `feature/w5-analyze` | pending | MFA/Whisper OSS |
| backend-compare | KAN-53 | `feature/w6-compare` | pending | Phase 3 |
| frontend-flows | KAN-54 | `feature/w7-frontend` | pending | Playwright required |
| docs-multilingual | KAN-55 | `feature/w8-docs-mfa` | pending | Phase 4 |

## Phase checklist

| Phase | Status | Success criteria (from plan) |
| ----- | ------ | ------------------------------ |
| 0 Foundation | done | .NET 10 + Vite + CI + MIT + reference.png (KAN-48) |
| 1 Reference + SVG | in progress | SVG rig done (KAN-50); reference audio/keyframes pending W4 |
| 2 Record + replay | pending | Analyze WAV; user pose differs on mispronunciation |
| 3 Compare + coaching | pending | Ghost overlay + coaching text |
| 4 Multilingual + MFA | pending | Locale packs + MFA Docker + PWA |

## Model & asset inventory

| Asset | Status | Path / notes |
| ----- | ------ | ------------- |
| Vocal tract reference | **done** | `assets/vocal-tract/reference.png` |
| Piper binary + en-US voice | **done** (local) | `models/piper/` via `scripts/provision-piper.ps1` (~97 MB); gitignored |
| Whisper cache | **reuse** | `%USERPROFILE%\.cache\whisper\` (~1.73 GB); not copied — see `docs/providers.md` |
| MFA Docker / models | pending | `workers/mfa/` (KAN-55, Phase 4) |
| CMU / G2P lexicon | pending | `VoiceRay.Core` (W4) |

## WIP policy (current wave)

**Active:** KAN-51 (W4 reference pipeline) — next

**Completed:** KAN-47 (W0 @ `0874ebc` files on integration); KAN-48–50 merged

**Proof queue:** `dotnet test` on integration after W4 slice

## Gate evidence

| Gate | Pre-work | Post-work |
| ---- | -------- | --------- |
| `dotnet build` | N/A; KAN-49: 0 warnings | KAN-48/49: pass (Release) |
| `dotnet test` | N/A; KAN-49: 7 passed | KAN-48: 4 passed |
| `npm run build` (client/) | N/A | KAN-48, KAN-50: pass |
| `npm run test` (client/) | N/A | KAN-50: 6 passed |
| Playwright | N/A | N/A (W7) |

## Merge order (into `feature/voiceray-mvp`)

1. ~~`feature/w1-scaffold`~~ @ `ed14360`
2. ~~`feature/w0-models`~~ W0 assets @ `0874ebc` (cherry-picked files, not branch merge)
3. ~~`feature/w2-api-contract`~~ @ `cbcd6bb`
4. ~~`feature/w3-vocal-tract-svg`~~ @ `6418dd8`
5. `feature/w4-reference-pipeline`
6. `feature/w5-analyze`
7. `feature/w6-compare`
8. `feature/w7-frontend`
9. `feature/w8-docs-mfa`
10. Single PR → `main`

## Blockers

| ID | Blocker | Owner | Next action |
| -- | ------- | ----- | ----------- |
| — | None | — | — |

## KAN-47 commit gate (feature/w0-models)

| Item | Status |
| ---- | ------ |
| Commit | `0874ebc` (NOTICE, `scripts/provision-piper.ps1`, `docs/providers.md`) |
| Piper smoke | `piper.exe --version` → 1.2.0 |
| Whisper | Cache audited; reuse only |

## KAN-49 commit gate (feature/w2-api-contract)

| Item | Status |
| ---- | ------ |
| Commit | `cbcd6bb` |
| Post-work `dotnet build` / `dotnet test` | Pass (7 tests, 0 warnings) |
| UI/API validation | `docs/api.md` + 501 stubs; OpenAPI `/openapi/v1.json` (Development) |

## KAN-50 commit gate (feature/w3-vocal-tract-svg)

| Item | Status |
| ---- | ------ |
| Commit | `8a2e6c5` |
| Post-work `npm run build` / `npm run test` | Pass (6 tests) |

## KAN-48 commit gate (feature/w1-scaffold)

| Item | Status |
| ---- | ------ |
| Commit | `ed14360` |
| Post-work `dotnet build` / `dotnet test` | Pass |
| Post-work `npm run build` / `npm test` | Pass |

## Agent notes

- **OSS speech config:** `Speech:Provider = Local` in appsettings; document Piper path in `docs/providers.md` (W8).
- **Azure:** Deferred; do not add SDK calls requiring keys in this epic.
- **Hot spots:** `.sln`, `VoiceRay.Core`, `VoiceRay.Api`, `client/vite.config`, `docs/status.md` (coordinator-owned reconciliation).
