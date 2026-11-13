"""Hand-recording contract (schema 0.1.x).

The Unity client writes this format (apps/unity/Assets/Suolingo/Capture/HandRecordingJsonWriter.cs).
The JSON Schema in contracts/hand-recording/ is generated from these models:

    uv run suolingo-recording export-schema
"""

from __future__ import annotations

from datetime import datetime
from typing import Annotated, Any, Literal

from pydantic import BaseModel, ConfigDict, Field, model_validator

SCHEMA_ID = "suolingo.hand-recording"
SCHEMA_VERSION = "0.1.0"
SUPPORTED_VERSION_PATTERN = r"^0\.1\.\d+$"

# OpenXR XR_EXT_hand_tracking joint order (26 joints).
JOINT_NAMES: tuple[str, ...] = (
    "palm",
    "wrist",
    "thumb_metacarpal",
    "thumb_proximal",
    "thumb_distal",
    "thumb_tip",
    "index_metacarpal",
    "index_proximal",
    "index_intermediate",
    "index_distal",
    "index_tip",
    "middle_metacarpal",
    "middle_proximal",
    "middle_intermediate",
    "middle_distal",
    "middle_tip",
    "ring_metacarpal",
    "ring_proximal",
    "ring_intermediate",
    "ring_distal",
    "ring_tip",
    "little_metacarpal",
    "little_proximal",
    "little_intermediate",
    "little_distal",
    "little_tip",
)
FINGER_NAMES: tuple[str, ...] = ("thumb", "index", "middle", "ring", "little")

JOINT_COUNT = len(JOINT_NAMES)
FINGER_COUNT = len(FINGER_NAMES)

Vec3 = Annotated[list[float], Field(min_length=3, max_length=3, description="[x, y, z] in meters")]
Quat = Annotated[list[float], Field(min_length=4, max_length=4, description="[x, y, z, w] unit quaternion")]


class _Strict(BaseModel):
    model_config = ConfigDict(extra="forbid")


class Pose(_Strict):
    position: Vec3
    rotation: Quat


class HandSample(_Strict):
    """One hand in one frame. Pose fields are present only when ``tracked`` is true."""

    tracked: bool = Field(description="SDK reports pose/joint data for this sample as usable.")
    high_confidence: bool
    input_state_valid: bool = Field(description="Pinch/pointer data usable.")
    system_gesture: bool = Field(description="Hand is performing an OS system gesture.")
    sample_time: float | None = Field(description="SDK sample timestamp (seconds, SDK clock).")
    hand_scale: float | None
    finger_high_confidence: Annotated[list[bool], Field(min_length=FINGER_COUNT, max_length=FINGER_COUNT)]
    pinch_strength: Annotated[list[float], Field(min_length=FINGER_COUNT, max_length=FINGER_COUNT)]
    root: Pose | None
    joint_positions: Annotated[list[Vec3], Field(min_length=JOINT_COUNT, max_length=JOINT_COUNT)] | None
    joint_rotations: Annotated[list[Quat], Field(min_length=JOINT_COUNT, max_length=JOINT_COUNT)] | None

    @model_validator(mode="after")
    def _pose_matches_tracking(self) -> HandSample:
        pose_fields = (self.root, self.joint_positions, self.joint_rotations)
        if self.tracked and any(f is None for f in pose_fields):
            raise ValueError("tracked hand must include root, joint_positions and joint_rotations")
        if not self.tracked and any(f is not None for f in pose_fields):
            raise ValueError("untracked hand must not include pose data (stale poses are not motion)")
        return self


class Frame(_Strict):
    t: float = Field(description="Seconds since capture start.")
    unity_frame: int
    head: Pose | None = Field(description="Head pose in the same space as hands; null when not tracked.")
    left: HandSample
    right: HandSample

    def hand(self, side: Literal["left", "right"]) -> HandSample:
        return self.left if side == "left" else self.right


class Producer(_Strict):
    app: str
    app_version: str
    unity_version: str
    platform: str
    device_model: str


class TrackingSource(_Strict):
    provider: str
    sdk_wrapper_version: str
    runtime_version: str
    joint_layout: Literal["openxr-ext-hand-tracking-26"]
    tracking_origin: str


class CoordinateSystem(_Strict):
    space: str
    handedness: Literal["left-handed"]
    units: Literal["meters"]
    axes: str
    rotation_format: Literal["quaternion-xyzw"]


class Capture(_Strict):
    countdown_seconds: float
    requested_duration_seconds: float
    start_realtime_seconds: float
    display_refresh_rate_hz: float | None
    end_reason: str
    time_base: str


class HandRecording(_Strict):
    schema_id: Literal["suolingo.hand-recording"] = Field(alias="schema")
    schema_version: str = Field(pattern=SUPPORTED_VERSION_PATTERN)
    recording_id: str = Field(min_length=1)
    created_at_utc: datetime
    participant_id: str | None = Field(description="Pseudonymous ID; never a real name.")
    expression_id: str | None
    notes: str | None
    producer: Producer
    tracking_source: TrackingSource
    coordinate_system: CoordinateSystem
    capture: Capture
    joints: list[str]
    fingers: list[str]
    frames: list[Frame] = Field(min_length=1)
    summary: dict[str, Any] | None = Field(
        default=None,
        description="Client-computed summary. Informational only; consumers recompute it from frames.",
    )

    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    @model_validator(mode="after")
    def _check_layout_and_time(self) -> HandRecording:
        if tuple(self.joints) != JOINT_NAMES:
            raise ValueError("joints must list the 26 OpenXR hand joints in canonical order")
        if tuple(self.fingers) != FINGER_NAMES:
            raise ValueError(f"fingers must be {list(FINGER_NAMES)}")
        previous = float("-inf")
        for index, frame in enumerate(self.frames):
            if frame.t < previous:
                raise ValueError(f"frame {index}: t={frame.t} is earlier than the previous frame")
            previous = frame.t
        return self
