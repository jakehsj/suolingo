import json

import pytest

from app.cli import SCHEMA_PATH, build_json_schema, main
from app.recordings.io import load_recording
from app.recordings.quality import analyze
from tests.factory import FIXTURE_PATH, make_recording


def test_committed_json_schema_is_current():
    assert SCHEMA_PATH.exists(), "run: uv run suolingo-recording export-schema"
    assert SCHEMA_PATH.read_text(encoding="utf-8") == build_json_schema()


def test_committed_fixture_matches_factory():
    assert json.loads(FIXTURE_PATH.read_text(encoding="utf-8")) == make_recording()
    load_recording(FIXTURE_PATH)


def test_cli_inspect(capsys):
    assert main(["inspect", str(FIXTURE_PATH)]) == 0
    out = capsys.readouterr().out
    assert "interruptions 1" in out
    assert "left_hand_tracking_interrupted:1" in out


def test_cli_inspect_invalid_file(tmp_path, capsys):
    bad = tmp_path / "bad.json"
    bad.write_text("{}", encoding="utf-8")
    assert main(["inspect", str(bad)]) == 1


UNITY_SAMPLE = FIXTURE_PATH.parent / "unity-writer-synthetic.json"


def test_unity_writer_output_matches_contract_and_summary():
    """The sample is produced by Unity's HandRecordingJsonWriter (Suolingo > Dev > Write Contract Sample)."""
    recording = load_recording(UNITY_SAMPLE)
    report = analyze(recording)
    stored = recording.summary
    assert stored is not None
    assert stored["frame_count"] == report.frame_count
    assert stored["duration_seconds"] == pytest.approx(report.duration_seconds, abs=1e-5)
    for side in ("left", "right"):
        mine = report.hand(side)
        theirs = stored[side]
        assert theirs["tracked_frames"] == mine.tracked_frames
        assert theirs["untracked_frames"] == mine.untracked_frames
        assert theirs["high_confidence_frames"] == mine.high_confidence_frames
        assert theirs["interruptions"] == mine.interruptions
        assert theirs["repeated_sample_frames"] == mine.repeated_sample_frames
        assert theirs["longest_untracked_seconds"] == pytest.approx(mine.longest_untracked_seconds, abs=1e-5)
