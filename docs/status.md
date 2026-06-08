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

## Agent notes

- Final PR per user policy; no per-ticket PRs.
- Phase 4 follow-ups (optional): locale packs, PWA manifest, live MFA HTTP client.
