import pytest

from app.recordings.quality import analyze
from app.schemas.recording import HandRecording
from tests.factory import make_recording


def report_for(**kwargs):
    return analyze(HandRecording.model_validate(make_recording(**kwargs)))


def test_interruption_is_reported_not_hidden():
    report = report_for(frame_count=40, rate_hz=72.0, left_lost=range(12, 20))
    left = report.left
    assert left.tracked_frames == 32
    assert left.untracked_frames == 8
    assert left.interruptions == 1
    assert len(left.gaps) == 1
    gap = left.gaps[0]
    assert gap.ended is True
    assert gap.frames == 8
    assert gap.seconds == pytest.approx(8 / 72, abs=1e-5)
    assert "left_hand_tracking_interrupted:1" in report.warnings
    assert report.right.interruptions == 0


def test_tracking_lost_until_end_is_an_unended_gap():
    report = report_for(frame_count=20, left_lost=None, right_lost=range(15, 20))
    gap = report.right.gaps[-1]
    assert gap.ended is False
    assert report.right.interruptions == 1


def test_leading_untracked_frames_are_not_interruptions():
    report = report_for(frame_count=20, left_lost=range(0, 5))
    assert report.left.interruptions == 0
    assert report.left.untracked_frames == 5
    assert "left_hand_tracking_interrupted" not in " ".join(report.warnings)


def test_never_tracked_hand_warns():
    report = report_for(frame_count=10, left_lost=range(0, 10))
    assert "left_hand_never_tracked" in report.warnings


def test_frame_timing():
    report = report_for(frame_count=73, rate_hz=72.0, left_lost=None)
    assert report.effective_rate_hz == pytest.approx(72.0, rel=1e-3)
    assert report.duration_seconds == pytest.approx(1.0, abs=1e-5)
    assert report.warnings == []
