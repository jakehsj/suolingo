from __future__ import annotations

from pathlib import Path

from app.schemas.recording import HandRecording


def load_recording(path: str | Path) -> HandRecording:
    """Parse and validate a recording file against the hand-recording contract."""
    return HandRecording.model_validate_json(Path(path).read_bytes())
