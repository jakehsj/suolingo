# Hand recording contract — `suolingo.hand-recording` 0.1.0 (draft)

Status: **draft, written from the Meta SDK 207.0.1 API, not yet confirmed with a recording from a real headset.**
The items listed under [Verify on device](#verify-on-device) must be confirmed using a real Quest 3 capture
before this version is considered stable.

A recording is one bounded capture (countdown → fixed-length capture) of head and hand tracking,
written by the Unity client as a single JSON document. It is used for development: reproducing attempts,
developing the backend without wearing a headset, and comparing algorithm versions on identical input.
Normal practice does not need to persist recordings.

- Machine-readable schema: [`hand-recording.schema.json`](hand-recording.schema.json) (generated; do not edit by hand).
- Source of truth for the schema: `services/matching/app/schemas/recording.py`.
- Writer: `apps/unity/Assets/Suolingo/Capture/HandRecordingJsonWriter.cs`.
- Example (synthetic, not real tracking data): [`tests/fixtures/recordings/`](../../tests/fixtures/recordings/).

Regenerate the schema after changing the models:

```bash
cd services/matching && uv run suolingo-recording export-schema
```

## Structure

```text
{
  "schema": "suolingo.hand-recording",
  "schema_version": "0.1.0",
  "recording_id", "created_at_utc", "participant_id" | null, "expression_id" | null, "notes" | null,
  "producer":          { app, app_version, unity_version, platform, device_model },
  "tracking_source":   { provider, sdk_wrapper_version, runtime_version, joint_layout, tracking_origin },
  "coordinate_system": { space, handedness, units, axes, rotation_format },
  "capture":           { countdown_seconds, requested_duration_seconds, start_realtime_seconds,
                         display_refresh_rate_hz | null, end_reason, time_base },
  "joints":  [26 joint names],
  "fingers": ["thumb", "index", "middle", "ring", "little"],
  "frames":  [ { t, unity_frame, head: Pose | null, left: HandSample, right: HandSample }, ... ],
  "summary": { ... } | null      // informational; consumers recompute it
}
```

`HandSample`:

| Field | Meaning |
| --- | --- |
| `tracked` | The SDK marks this sample's hand pose as usable (`HandStatus.HandTracked`). |
| `high_confidence` | Overall hand confidence is `High`. |
| `input_state_valid` | Pinch/pointer data usable. |
| `system_gesture` | The hand is performing a system gesture (e.g. the Meta menu gesture). |
| `sample_time` | SDK sample timestamp in seconds (SDK clock) or `null`. |
| `hand_scale` | SDK hand scale estimate or `null`. |
| `finger_high_confidence[5]`, `pinch_strength[5]` | Per-finger values in `fingers` order. |
| `root` | Hand root pose, or `null` when untracked. |
| `joint_positions[26]` | `[x, y, z]` meters, in `joints` order, or `null` when untracked. |
| `joint_rotations[26]` | `[x, y, z, w]` quaternions, in `joints` order, or `null` when untracked. |

## Rules

1. **Tracking interruptions are explicit.** If `tracked` is `false`, `root`, `joint_positions`, and
   `joint_rotations` must be `null`. The SDK may keep returning the last known pose after tracking is lost.
   That pose must never be recorded as valid, stationary motion. Validators reject untracked samples
   that contain pose data.
2. **Measured data only.** Poses come from `OVRPlugin.GetHandState` (raw tracking), not from
   `OVRSkeleton`/`SyntheticHand` transforms that interaction components may modify.
3. **One space for everything.** Head, hand root, and joints are in the same tracking space:
   OVRCameraRig tracking space, converted to Unity conventions (left-handed, meters, +Y up, +Z forward).
   `tracking_source.tracking_origin` records the origin mode, such as `Stage` or `EyeLevel`.
4. **Timing.** `t` is seconds since capture start, based on Unity's `realtimeSinceStartup` when the frame was read.
   It is non-decreasing. `sample_time` is the SDK's own timestamp. The same `sample_time` on consecutive tracked
   frames means that the SDK did not deliver a new sample. One frame is recorded per rendered Unity frame,
   so the rate follows the display refresh rate (for example, 72/90/120 Hz).
5. **Joint layout** is OpenXR `XR_EXT_hand_tracking` (26 joints) and requires
   `OVRRuntimeSettings.HandSkeletonVersion = OpenXR`. The client refuses to record another layout.
6. **Participant IDs are pseudonyms.** Never write a person's name in `participant_id` or `notes`.
7. **Versioning.** Additive or clarifying changes use `0.1.x`. Field renames or semantic changes require a new
   minor version and validator support for the new version.

## Verify on device

Record a few attempts on a Quest 3 and check the following. Record the results here and in `docs/PROJECT_PLAN.md`.

- [ ] Joint positions are in the same space as `head` (for example, the hands appear in front of and below the head,
      not mirrored on Z).
- [ ] Positions are metric world scale (a typical wrist-to-middle-tip distance is about 0.17–0.20 m), not
      normalized by `hand_scale`.
- [ ] `tracked` becomes `false` when a hand leaves the view or is occluded. Check how long the SDK
      reports a lost hand as tracked.
- [ ] `sample_time` advances by about one frame interval and indicates repeated samples.
- [ ] Achieved frame rate and file size for a 4 s capture with `handTrackingFrequency = HIGH`.
- [ ] Behavior while the system gesture is active.
- [ ] Reported `tracking_origin` value under OpenXR (FloorLevel is mapped to Stage).
