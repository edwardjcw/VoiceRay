# VoiceRay Development Workflow

Use these instructions for any task in the VoiceRay repository — new features, bug fixes, refactors, or UI changes.

## Procedure

### 1. Branch
Create a separate git branch for the task before making any changes.

### 2. Pre-Work Test Gate (Mandatory)
Before making **any** code changes, run the relevant test suite for the layer you are modifying:

Backend (.NET 10 / F#):
```
dotnet test
```

Frontend (Vite / JS):
```
npm run test
# (run within client/ directory)
```
If any test fails, stop feature work and stabilize the suite first, or explicitly document and get approval for existing red tests.
Confirm the baseline build quality gate is clean: `dotnet build` and `npm run build` must report **0 errors and 0 warnings** before feature changes.

### 3. Understand the Architecture
Always reference `docs/plan.md` as the source of truth for application architecture. F# handles pure logic (phonemes, TTS, API), while JS handles the UI and SVG animation.

### 4. Make the Smallest Testable Change First
If a task requires multiple changes, identify the smallest independently testable unit and implement that first. Run tests before moving on. This makes it easier to isolate failures.

### 5. Write Unit Tests and Integration Tests (Playwright)
Create unit tests for every change.

Backend tests live in the F# `.Tests` projects.
Frontend tests live in `client/tests` (if applicable).
The user should never have to discover something isn't working as intended in the frontend. Playwright tests should discover it first.

### 5a. UI/API Integration Checklist
If your task changes the API response shape or the frontend UI behavior, you must:

1. Update the OpenAPI/JSON contract specification in `docs/api.md`.
2. Add or update backend tests validating the payload shape.
3. Add or update frontend UI mock tests if available, or manually verify the flow locally.
4. Run both backend and frontend suites.

### 6. Update Infrastructure/CI
If a change affects the build pipeline, Azure configuration, or Docker deployment, update the corresponding files in `.github/workflows/` or `VoiceRay.Infrastructure/`.

### 7. Add and Validate Examples
For backend phoneme maps and API logic, ensure that the demo reference inputs in `docs/articulatory-model.md` still render successfully.
Run the local dev servers (`dotnet run` and `npm run dev`) and verify the UI rendering.

### 8. Update Documentation
- Update `docs/status.md` **as you go** — after each step, not just at the end. This lets another agent resume from where you left off if you time out or get stuck.
- Update relevant `README.md` files if behavior changed.
- Update `docs/plan.md` or `docs/architecture.md` if the overarching design changed.

### 9. Post-Work Test Gate (Mandatory)
After finishing code changes, run the tests again:
```
dotnet test
```
And for the frontend (within `client/`):
```
npm run build
npm run test
```
All tests, including playwright tests, must pass before considering the task done.

### 10. Rule Compliance Gate (Mandatory)
Do not close a task unless every required step in this file was completed.  
If a step is not applicable, explicitly state why in `docs/status.md`.

### 11. Pre-Commit/PR Enforcement Gate (Mandatory)
Before creating a commit or PR, verify and document all of the following in `docs/status.md`:
- Branch name used for the task.
- Pre-work gates completed (`dotnet test`, `npm run build` with 0 warnings).
- Post-work gates completed (`dotnet test`, `npm run build` with 0 warnings).
- UI/API validation completed (or explicit N/A reason).
- Files/docs updated for behavior/workflow changes.

If any required item above is missing, do **not** commit and do **not** open a PR yet.

### 12. Create a Pull Request
When the task is complete and all tests pass, create a commit and then create a pull request in the same execution flow.

### 12a. No-Handoff Automation Rule (Mandatory)
Once all mandatory gates in this file are green, the agent must automatically continue through commit + PR creation in the same run.

- Do **not** pause to ask the user whether to commit or open a PR.
- Do **not** stop at "implementation complete" or "ready to commit".
- Only stop and ask the user if a true blocker exists (e.g., failing gates that cannot be resolved, missing credentials/permissions, or an explicit user instruction to pause).
- If a blocker prevents commit/PR, record the exact blocker and next action in `docs/status.md`.

This rule exists to prevent handoff churn and requires agents to finish the full workflow autonomously.
