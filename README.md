# VoiceRay

Open-source web app for pronunciation practice: an F# (.NET 10) backend handles phonetics, TTS, and alignment; a Vite JavaScript client animates a layered sagittal vocal-tract SVG.

## Stack

| Layer | Project |
| ----- | ------- |
| API | `src/VoiceRay.Api` |
| Domain | `src/VoiceRay.Core` |
| Speech / audio adapters | `src/VoiceRay.Infrastructure` |
| UI | `client/` (Vite) |

Speech provider is server-configured (`Speech:Provider` in appsettings). The MVP epic uses **Local** OSS (Piper, MFA) only.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (see `global.json`)
- [Node.js](https://nodejs.org/) 20+ for the client

## Quick start

```bash
# Backend
dotnet run --project src/VoiceRay.Api

# Frontend (separate terminal)
cd client
npm install
npm run dev
```

Open http://localhost:5173 — the dev server proxies `/api` to the API on port 5000.

Optional: copy `client/.env.example` to `client/.env` and set `VITE_API_BASE_URL`.

## Build & test

```bash
dotnet build
dotnet test

cd client
npm run build
```

CI runs the same steps (see `.github/workflows/ci.yml`).

## Documentation

- [Architecture & phases](docs/plan.md)
- [Development workflow](docs/instructions.md)
- [Execution status](docs/status.md)

## License

MIT — see [LICENSE](LICENSE).
