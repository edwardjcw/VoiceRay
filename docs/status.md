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

All ticket branches merged. Release step: PR → `main`.

## Blockers

None.

## KAN-54 commit gate

| Item | Status |
| ---- | ------ |
| Commit | `1018650` |
| Post-work npm | 9 unit + 4 Playwright |
| UI | Practice/record/compare, deviceBanner, ghost overlay |

## Agent notes

- Final PR per user policy; no per-ticket PRs.
- Phase 4 follow-ups (optional): locale packs, PWA manifest, live MFA HTTP client.
