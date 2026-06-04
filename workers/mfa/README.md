# VoiceRay MFA worker (Phase 4 stub)

Optional **Montreal Forced Aligner** sidecar for self-hosted forced alignment. The .NET API calls this over HTTP when `Speech:Alignment:Mfa:WorkerUrl` is set (see [`docs/providers.md`](../../docs/providers.md)).

## MVP status (KAN-55)

This directory is a **Docker stub** only:

- Health endpoint on port **8765**
- Documented planned `POST /v1/align` contract
- No MFA models or real alignment yet

`VoiceRay.Infrastructure` still uses in-process `mfa-stub` timing when the worker URL is empty.

## Build and run

```bash
docker compose up --build
```

Health check:

```bash
curl http://localhost:8765/health
```

Configure the API (when client is implemented):

```json
"Mfa": { "WorkerUrl": "http://localhost:8765" }
```

## Planned production image

Replace the stub `Dockerfile` with:

1. Base image with MFA conda environment
2. Downloaded acoustic + dictionary models per locale under `/models`
3. Long-running HTTP service wrapping `mfa align` (WAV + transcript → TextGrid → IPA segments)

Model paths stay **out of git**; document download steps in this README when implemented.

## HTTP contract (target)

### `GET /health`

Returns service identity for compose healthcheck.

### `POST /v1/align`

Request:

```json
{
  "locale": "en-US",
  "text": "pat",
  "audioWavBase64": "<base64>"
}
```

Response:

```json
{
  "engine": "mfa",
  "phonemes": [
    { "ipa": "p", "startMs": 0, "endMs": 80 }
  ]
}
```

## License note

MFA and its models carry separate licenses from VoiceRay (MIT). Record versions in `NOTICE` when the real image ships.
