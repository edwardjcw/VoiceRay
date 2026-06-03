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
| scaffold-solution | KAN-48 | `feature/w1-scaffold` | in_progress | Phase 0 |
| vocal-tract-svg | KAN-50 | `feature/w3-vocal-tract-svg` | pending | Needs W1 client folder |
| api-contract | KAN-49 | `feature/w2-api-contract` | pending | After W1 |
| backend-reference | KAN-51 | `feature/w4-reference-pipeline` | pending | Piper from W0 |
| backend-analyze | KAN-52 | `feature/w5-analyze` | pending | MFA/Whisper OSS |
| backend-compare | KAN-53 | `feature/w6-compare` | pending | Phase 3 |
| frontend-flows | KAN-54 | `feature/w7-frontend` | pending | Playwright required |
| docs-multilingual | KAN-55 | `feature/w8-docs-mfa` | pending | Phase 4 |

## Phase checklist

| Phase | Status | Success criteria (from plan) |
| ----- | ------ | ------------------------------ |
| 0 Foundation | in_progress | .NET 10 + Vite + CI + MIT + reference.png |
| 1 Reference + SVG | pending | Hear reference; sagittal animates demo words |
| 2 Record + replay | pending | Analyze WAV; user pose differs on mispronunciation |
| 3 Compare + coaching | pending | Ghost overlay + coaching text |
| 4 Multilingual + MFA | pending | Locale packs + MFA Docker + PWA |

## Model & asset inventory

| Asset | Status | Path / notes |
| ----- | ------ | ------------- |
| Vocal tract reference | **done** | `assets/vocal-tract/reference.png` |
| Piper binary + en-US voice | pending | `models/piper/` (KAN-47) |
| Whisper cache | pending audit | Reuse `%USERPROFILE%\.cache\whisper\` (base.en, medium.en from CloneMyVoice) |
| MFA Docker / models | pending | `workers/mfa/` (KAN-55, Phase 4) |
| CMU / G2P lexicon | pending | `VoiceRay.Core` (W4) |

## WIP policy (current wave)

**Active (max 2):**

1. KAN-47 (W0 models) — `feature/w0-models`
2. KAN-48 (W1 scaffold) — `feature/w1-scaffold`

**Next queue:** KAN-49, KAN-50 (parallel after W1 merges)

**Proof queue:** empty (baseline `dotnet test` after W1 scaffold lands on integration branch)

**Integration queue:** merge `feature/w0-models` and `feature/w1-scaffold` → `feature/voiceray-mvp` when each is proof-green

## Gate evidence

| Gate | Pre-work | Post-work |
| ---- | -------- | --------- |
| `dotnet build` | — | — |
| `dotnet test` | — | — |
| `npm run build` (client/) | — | — |
| Playwright | — | — |

## Merge order (into `feature/voiceray-mvp`)

1. `feature/w1-scaffold` (foundation)
2. `feature/w0-models` (can parallel; merge after or with W1 if no conflicts)
3. `feature/w2-api-contract`
4. `feature/w3-vocal-tract-svg` (parallel with W2 if hot-spots OK)
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

## Agent notes

- **OSS speech config:** `Speech:Provider = Local` in appsettings; document Piper path in `docs/providers.md` (W8).
- **Azure:** Deferred; do not add SDK calls requiring keys in this epic.
- **Hot spots:** `.sln`, `VoiceRay.Core`, `VoiceRay.Api`, `client/vite.config`, `docs/status.md` (coordinator-owned reconciliation).
