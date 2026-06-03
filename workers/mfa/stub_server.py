"""Minimal HTTP stub for VoiceRay MFA worker (Phase 4)."""

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

app = FastAPI(title="VoiceRay MFA Worker", version="0.0.0-stub")


@app.get("/health")
def health():
    return {"status": "stub", "service": "voiceray-mfa"}


class AlignRequest(BaseModel):
    locale: str
    text: str
    audioWavBase64: str


@app.post("/v1/align")
def align(_body: AlignRequest):
    raise HTTPException(
        status_code=501,
        detail="MFA alignment not implemented; use in-process mfa-stub in VoiceRay.Api",
    )
