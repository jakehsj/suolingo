using System;
using UnityEngine;

namespace Suolingo.Tracking
{
    /// <summary>
    /// One hand's measured tracking state for a single sample. SDK-independent.
    /// When <see cref="Tracked"/> is false the pose fields must not be interpreted:
    /// they may contain a stale last-known pose.
    /// </summary>
    [Serializable]
    public sealed class HandObservation
    {
        /// <summary>The SDK reports this sample's hand pose and joint data as usable.</summary>
        public bool Tracked;

        /// <summary>Overall hand confidence reported by the SDK is high.</summary>
        public bool HighConfidence;

        /// <summary>Pointer/pinch input data reported as usable.</summary>
        public bool InputStateValid;

        /// <summary>The hand is currently performing an OS system gesture (e.g. menu).</summary>
        public bool SystemGestureInProgress;

        /// <summary>SDK sample timestamp in seconds (SDK clock). NaN when unavailable.</summary>
        public double SampleTime = double.NaN;

        /// <summary>SDK-estimated hand scale relative to its reference hand. NaN when unavailable.</summary>
        public float HandScale = float.NaN;

        public Vector3 RootPosition;
        public Quaternion RootRotation = Quaternion.identity;

        public readonly Vector3[] JointPositions = new Vector3[HandJoints.Count];
        public readonly Quaternion[] JointRotations = new Quaternion[HandJoints.Count];
        public readonly bool[] FingerHighConfidence = new bool[HandJoints.FingerCount];
        public readonly float[] PinchStrength = new float[HandJoints.FingerCount];

        public void Clear()
        {
            Tracked = false;
            HighConfidence = false;
            InputStateValid = false;
            SystemGestureInProgress = false;
            SampleTime = double.NaN;
            HandScale = float.NaN;
            RootPosition = Vector3.zero;
            RootRotation = Quaternion.identity;
            Array.Clear(JointPositions, 0, JointPositions.Length);
            for (var i = 0; i < JointRotations.Length; i++) JointRotations[i] = Quaternion.identity;
            Array.Clear(FingerHighConfidence, 0, FingerHighConfidence.Length);
            Array.Clear(PinchStrength, 0, PinchStrength.Length);
        }

        public void CopyFrom(HandObservation other)
        {
            Tracked = other.Tracked;
            HighConfidence = other.HighConfidence;
            InputStateValid = other.InputStateValid;
            SystemGestureInProgress = other.SystemGestureInProgress;
            SampleTime = other.SampleTime;
            HandScale = other.HandScale;
            RootPosition = other.RootPosition;
            RootRotation = other.RootRotation;
            Array.Copy(other.JointPositions, JointPositions, HandJoints.Count);
            Array.Copy(other.JointRotations, JointRotations, HandJoints.Count);
            Array.Copy(other.FingerHighConfidence, FingerHighConfidence, HandJoints.FingerCount);
            Array.Copy(other.PinchStrength, PinchStrength, HandJoints.FingerCount);
        }
    }
}
