from __future__ import annotations

from fastapi import APIRouter

from app import __version__
from app.recordings.quality import analyze
from app.schemas.recording import SCHEMA_VERSION, HandRecording

router = APIRouter()


@router.get("/health")
def health() -> dict:
    return {
        "status": "ok",
        "service": "suolingo-matching",
        "version": __version__,
        "contracts": {"hand_recording": SCHEMA_VERSION},
        # No evaluation (mock or real) exists yet; added in Milestone 2.
        "evaluation": "not-implemented",
    }


@router.post("/v1/recordings/inspect")
def inspect_recording(recording: HandRecording) -> dict:
    """Validate a recording and report input quality. Development aid; this is not sign evaluation."""
    return {"kind": "input-quality-report", "report": analyze(recording).to_dict()}
