using System;
using System.Globalization;
using System.Text;
using Suolingo.Tracking;
using UnityEngine;

namespace Suolingo.Capture
{
    /// <summary>
    /// Serializes <see cref="HandRecording"/> to the JSON format described in
    /// contracts/hand-recording. Hand-written (rather than JsonUtility) because the
    /// format needs nulls, nested arrays and culture-invariant numbers.
    /// </summary>
    public static class HandRecordingJsonWriter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Write(HandRecording recording, RecordingSummary summary = null)
        {
            summary ??= RecordingSummary.Compute(recording.Frames, recording.CaptureStartRealtime);
            var sb = new StringBuilder(4096 + recording.Frames.Count * 3600);

            sb.Append('{');
            Prop(sb, "schema", HandRecording.SchemaId); sb.Append(',');
            Prop(sb, "schema_version", HandRecording.SchemaVersion); sb.Append(',');
            Prop(sb, "recording_id", recording.RecordingId); sb.Append(',');
            Prop(sb, "created_at_utc", recording.CreatedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", Inv)); sb.Append(',');
            Prop(sb, "participant_id", recording.ParticipantId); sb.Append(',');
            Prop(sb, "expression_id", recording.ExpressionId); sb.Append(',');
            Prop(sb, "notes", recording.Notes); sb.Append(',');

            Key(sb, "producer"); sb.Append('{');
            Prop(sb, "app", recording.Producer.App); sb.Append(',');
            Prop(sb, "app_version", recording.Producer.AppVersion); sb.Append(',');
            Prop(sb, "unity_version", recording.Producer.UnityVersion); sb.Append(',');
            Prop(sb, "platform", recording.Producer.Platform); sb.Append(',');
            Prop(sb, "device_model", recording.Producer.DeviceModel);
            sb.Append("},");

            Key(sb, "tracking_source"); sb.Append('{');
            Prop(sb, "provider", recording.Source.Provider); sb.Append(',');
            Prop(sb, "sdk_wrapper_version", recording.Source.SdkWrapperVersion); sb.Append(',');
            Prop(sb, "runtime_version", recording.Source.RuntimeVersion); sb.Append(',');
            Prop(sb, "joint_layout", recording.Source.JointLayout); sb.Append(',');
            Prop(sb, "tracking_origin", recording.Source.TrackingOrigin);
            sb.Append("},");

            Key(sb, "coordinate_system"); sb.Append('{');
            Prop(sb, "space", recording.Source.Space); sb.Append(',');
            Prop(sb, "handedness", "left-handed"); sb.Append(',');
            Prop(sb, "units", "meters"); sb.Append(',');
            Prop(sb, "axes", "+x right, +y up, +z forward"); sb.Append(',');
            Prop(sb, "rotation_format", "quaternion-xyzw");
            sb.Append("},");

            Key(sb, "capture"); sb.Append('{');
            Prop(sb, "countdown_seconds", recording.CountdownSeconds); sb.Append(',');
            Prop(sb, "requested_duration_seconds", recording.RequestedDurationSeconds); sb.Append(',');
            Prop(sb, "start_realtime_seconds", recording.CaptureStartRealtime); sb.Append(',');
            Prop(sb, "display_refresh_rate_hz", recording.DisplayRefreshRateHz); sb.Append(',');
            Prop(sb, "end_reason", recording.EndReason); sb.Append(',');
            Prop(sb, "time_base", "t = Unity realtimeSinceStartup at sampling minus start_realtime_seconds");
            sb.Append("},");

            Key(sb, "joints"); StringArray(sb, HandJoints.Names); sb.Append(',');
            Key(sb, "fingers"); StringArray(sb, HandJoints.FingerNames); sb.Append(',');

            Key(sb, "frames"); sb.Append('[');
            for (var i = 0; i < recording.Frames.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteFrame(sb, recording.Frames[i], recording.CaptureStartRealtime);
            }
            sb.Append("],");

            Key(sb, "summary"); WriteSummary(sb, summary);
            sb.Append('}');
            return sb.ToString();
        }

        private static void WriteFrame(StringBuilder sb, TrackingFrame f, double start)
        {
            sb.Append('{');
            Prop(sb, "t", f.RealtimeSeconds - start); sb.Append(',');
            Prop(sb, "unity_frame", f.UnityFrame); sb.Append(',');
            Key(sb, "head");
            if (f.HeadValid)
            {
                sb.Append('{');
                Key(sb, "position"); Vec3(sb, f.HeadPosition); sb.Append(',');
                Key(sb, "rotation"); Quat(sb, f.HeadRotation);
                sb.Append('}');
            }
            else
            {
                sb.Append("null");
            }
            sb.Append(',');
            Key(sb, "left"); WriteHand(sb, f.Left); sb.Append(',');
            Key(sb, "right"); WriteHand(sb, f.Right);
            sb.Append('}');
        }

        private static void WriteHand(StringBuilder sb, HandObservation h)
        {
            sb.Append('{');
            Prop(sb, "tracked", h.Tracked); sb.Append(',');
            Prop(sb, "high_confidence", h.HighConfidence); sb.Append(',');
            Prop(sb, "input_state_valid", h.InputStateValid); sb.Append(',');
            Prop(sb, "system_gesture", h.SystemGestureInProgress); sb.Append(',');
            Prop(sb, "sample_time", h.SampleTime); sb.Append(',');
            Prop(sb, "hand_scale", h.HandScale); sb.Append(',');

            Key(sb, "finger_high_confidence"); sb.Append('[');
            for (var i = 0; i < h.FingerHighConfidence.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(h.FingerHighConfidence[i] ? "true" : "false");
            }
            sb.Append("],");

            Key(sb, "pinch_strength"); sb.Append('[');
            for (var i = 0; i < h.PinchStrength.Length; i++)
            {
                if (i > 0) sb.Append(',');
                Num(sb, h.PinchStrength[i], "0.###");
            }
            sb.Append("],");

            if (h.Tracked)
            {
                Key(sb, "root"); sb.Append('{');
                Key(sb, "position"); Vec3(sb, h.RootPosition); sb.Append(',');
                Key(sb, "rotation"); Quat(sb, h.RootRotation);
                sb.Append("},");
                Key(sb, "joint_positions"); sb.Append('[');
                for (var i = 0; i < HandJoints.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Vec3(sb, h.JointPositions[i]);
                }
                sb.Append("],");
                Key(sb, "joint_rotations"); sb.Append('[');
                for (var i = 0; i < HandJoints.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Quat(sb, h.JointRotations[i]);
                }
                sb.Append(']');
            }
            else
            {
                sb.Append("\"root\":null,\"joint_positions\":null,\"joint_rotations\":null");
            }
            sb.Append('}');
        }

        private static void WriteSummary(StringBuilder sb, RecordingSummary s)
        {
            sb.Append('{');
            Prop(sb, "frame_count", s.FrameCount); sb.Append(',');
            Prop(sb, "duration_seconds", s.DurationSeconds); sb.Append(',');
            Prop(sb, "mean_frame_interval_seconds", s.MeanFrameIntervalSeconds); sb.Append(',');
            Prop(sb, "max_frame_interval_seconds", s.MaxFrameIntervalSeconds); sb.Append(',');
            Key(sb, "left"); WriteHandSummary(sb, s.Left); sb.Append(',');
            Key(sb, "right"); WriteHandSummary(sb, s.Right);
            sb.Append('}');
        }

        private static void WriteHandSummary(StringBuilder sb, RecordingSummary.HandSummary h)
        {
            sb.Append('{');
            Prop(sb, "tracked_frames", h.TrackedFrames); sb.Append(',');
            Prop(sb, "untracked_frames", h.UntrackedFrames); sb.Append(',');
            Prop(sb, "high_confidence_frames", h.HighConfidenceFrames); sb.Append(',');
            Prop(sb, "interruptions", h.Interruptions); sb.Append(',');
            Prop(sb, "longest_untracked_seconds", h.LongestUntrackedSeconds); sb.Append(',');
            Prop(sb, "repeated_sample_frames", h.RepeatedSampleFrames);
            sb.Append('}');
        }

        // --- primitives -------------------------------------------------------

        private static void Key(StringBuilder sb, string key)
        {
            Str(sb, key);
            sb.Append(':');
        }

        private static void Prop(StringBuilder sb, string key, string value)
        {
            Key(sb, key);
            if (value == null) sb.Append("null");
            else Str(sb, value);
        }

        private static void Prop(StringBuilder sb, string key, bool value)
        {
            Key(sb, key);
            sb.Append(value ? "true" : "false");
        }

        private static void Prop(StringBuilder sb, string key, int value)
        {
            Key(sb, key);
            sb.Append(value.ToString(Inv));
        }

        private static void Prop(StringBuilder sb, string key, double value)
        {
            Key(sb, key);
            Num(sb, value, "0.######");
        }

        internal static void Num(StringBuilder sb, double value, string format)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) sb.Append("null");
            else sb.Append(value.ToString(format, Inv));
        }

        private static void Vec3(StringBuilder sb, Vector3 v)
        {
            sb.Append('[');
            Num(sb, v.x, "0.#####"); sb.Append(',');
            Num(sb, v.y, "0.#####"); sb.Append(',');
            Num(sb, v.z, "0.#####");
            sb.Append(']');
        }

        private static void Quat(StringBuilder sb, Quaternion q)
        {
            sb.Append('[');
            Num(sb, q.x, "0.######"); sb.Append(',');
            Num(sb, q.y, "0.######"); sb.Append(',');
            Num(sb, q.z, "0.######"); sb.Append(',');
            Num(sb, q.w, "0.######");
            sb.Append(']');
        }

        private static void StringArray(StringBuilder sb, string[] values)
        {
            sb.Append('[');
            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(',');
                Str(sb, values[i]);
            }
            sb.Append(']');
        }

        private static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", Inv));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
