"""Input-quality analysis of a recording.

This describes whether the captured data is *interpretable* (tracking coverage,
interruptions, frame timing). It is not a sign evaluation and says nothing about
whether a sign was performed correctly.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass, field
from typing import Literal

from app.schemas.recording import HandRecording

Side = Literal["left", "right"]

# Thresholds for warnings only; they are development heuristics, not validated limits.
LARGE_FRAME_GAP_SECONDS = 0.1
SHORT_CAPTURE_RATIO = 0.9


@dataclass
class TrackingGap:
    start_t: float
    end_t: float
    frames: int
    ended: bool  # False when tracking had not resumed by the end of the capture

    @property
    def seconds(self) -> float:
        return self.end_t - self.start_t


@dataclass
class HandQuality:
    tracked_frames: int = 0
    untracked_frames: int = 0
    high_confidence_frames: int = 0
    interruptions: int = 0
    longest_untracked_seconds: float = 0.0
    repeated_sample_frames: int = 0
    gaps: list[TrackingGap] = field(default_factory=list)

    def tracked_ratio(self, frame_count: int) -> float:
        return self.tracked_frames / frame_count if frame_count else 0.0


@dataclass
class QualityReport:
    recording_id: str
    frame_count: int
    duration_seconds: float
    requested_duration_seconds: float
    mean_frame_interval_seconds: float | None
    max_frame_interval_seconds: float | None
    effective_rate_hz: float | None
    head_tracked_frames: int
    left: HandQuality
    right: HandQuality
    warnings: list[str]

    def hand(self, side: Side) -> HandQuality:
        return self.left if side == "left" else self.right

    def to_dict(self) -> dict:
        data = asdict(self)
        for side in ("left", "right"):
            hand = self.hand(side)
            data[side]["tracked_ratio"] = round(hand.tracked_ratio(self.frame_count), 4)
            for gap_dict, gap in zip(data[side]["gaps"], hand.gaps, strict=True):
                gap_dict["seconds"] = gap.seconds
        return data


def _hand_quality(recording: HandRecording, side: Side) -> HandQuality:
    q = HandQuality()
    ever_tracked = False
    previous_tracked = False
    previous_sample: float | None = None
    gap_start: tuple[int, float] | None = None  # (frame index, t)

    frames = recording.frames
    for index, frame in enumerate(frames):
        hand = frame.hand(side)
        if hand.tracked:
            q.tracked_frames += 1
            if hand.high_confidence:
                q.high_confidence_frames += 1
            if hand.sample_time is not None and hand.sample_time == previous_sample:
                q.repeated_sample_frames += 1
            previous_sample = hand.sample_time
            if gap_start is not None:
                q.gaps.append(TrackingGap(gap_start[1], frame.t, index - gap_start[0], ended=True))
                gap_start = None
            ever_tracked = True
        else:
            q.untracked_frames += 1
            if ever_tracked and previous_tracked:
                q.interruptions += 1
            if gap_start is None:
                gap_start = (index, frame.t)
        previous_tracked = hand.tracked

    if gap_start is not None:
        q.gaps.append(TrackingGap(gap_start[1], frames[-1].t, len(frames) - gap_start[0], ended=False))

    q.longest_untracked_seconds = max((g.seconds for g in q.gaps), default=0.0)
    return q


def analyze(recording: HandRecording) -> QualityReport:
    frames = recording.frames
    intervals = [b.t - a.t for a, b in zip(frames, frames[1:], strict=False)]
    mean_interval = sum(intervals) / len(intervals) if intervals else None
    max_interval = max(intervals) if intervals else None
    duration = frames[-1].t if frames else 0.0

    left = _hand_quality(recording, "left")
    right = _hand_quality(recording, "right")

    warnings: list[str] = []
    for side, q in (("left", left), ("right", right)):
        if q.tracked_frames == 0:
            warnings.append(f"{side}_hand_never_tracked")
        elif q.interruptions:
            warnings.append(f"{side}_hand_tracking_interrupted:{q.interruptions}")
        if q.repeated_sample_frames:
            warnings.append(f"{side}_hand_repeated_samples:{q.repeated_sample_frames}")
    if max_interval is not None and max_interval > LARGE_FRAME_GAP_SECONDS:
        warnings.append(f"large_frame_interval:{max_interval:.3f}s")
    requested = recording.capture.requested_duration_seconds
    if recording.capture.end_reason != "completed":
        warnings.append(f"capture_{recording.capture.end_reason}")
    elif duration < requested * SHORT_CAPTURE_RATIO:
        warnings.append(f"capture_shorter_than_requested:{duration:.2f}s<{requested:.2f}s")
    head_tracked = sum(1 for f in frames if f.head is not None)
    if head_tracked < len(frames):
        warnings.append(f"head_untracked_frames:{len(frames) - head_tracked}")

    return QualityReport(
        recording_id=recording.recording_id,
        frame_count=len(frames),
        duration_seconds=duration,
        requested_duration_seconds=requested,
        mean_frame_interval_seconds=mean_interval,
        max_frame_interval_seconds=max_interval,
        effective_rate_hz=1.0 / mean_interval if mean_interval else None,
        head_tracked_frames=head_tracked,
        left=left,
        right=right,
        warnings=warnings,
    )
