import copy

import pytest
from pydantic import ValidationError

from app.schemas.recording import HandRecording
from tests.factory import make_recording


def test_valid_recording_parses():
    recording = HandRecording.model_validate(make_recording())
    assert len(recording.frames) == 40
    assert recording.frames[12].left.tracked is False
    assert recording.frames[12].left.joint_positions is None


def test_untracked_hand_with_pose_is_rejected():
    data = make_recording()
    stale = copy.deepcopy(data["frames"][11]["left"])
    stale["tracked"] = False  # stale pose must not masquerade as motion
    data["frames"][12]["left"] = stale
    with pytest.raises(ValidationError, match="untracked hand must not include pose data"):
        HandRecording.model_validate(data)


def test_tracked_hand_without_joints_is_rejected():
    data = make_recording()
    data["frames"][0]["right"]["joint_positions"] = None
    with pytest.raises(ValidationError, match="tracked hand must include"):
        HandRecording.model_validate(data)


def test_wrong_joint_count_is_rejected():
    data = make_recording()
    data["frames"][0]["right"]["joint_positions"].pop()
    with pytest.raises(ValidationError):
        HandRecording.model_validate(data)


def test_non_monotonic_time_is_rejected():
    data = make_recording()
    data["frames"][5]["t"] = 0.0
    with pytest.raises(ValidationError, match="earlier than the previous frame"):
        HandRecording.model_validate(data)


def test_unsupported_schema_version_is_rejected():
    data = make_recording()
    data["schema_version"] = "1.0.0"
    with pytest.raises(ValidationError):
        HandRecording.model_validate(data)


def test_unknown_fields_are_rejected():
    data = make_recording()
    data["frames"][0]["left"]["extra"] = 1
    with pytest.raises(ValidationError):
        HandRecording.model_validate(data)
