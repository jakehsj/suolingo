using System;
using UnityEngine;

namespace Suolingo.Tracking
{
    /// <summary>
    /// A snapshot of head and hand tracking for one application frame, expressed in
    /// the tracking space described by <see cref="TrackingSourceInfo"/>.
    /// </summary>
    [Serializable]
    public sealed class TrackingFrame
    {
        /// <summary>Unity frame number (Time.frameCount) at which the sample was read.</summary>
        public int UnityFrame;

        /// <summary>Time.realtimeSinceStartupAsDouble at which the sample was read.</summary>
        public double RealtimeSeconds;

        public bool HeadValid;
        public Vector3 HeadPosition;
        public Quaternion HeadRotation = Quaternion.identity;

        public readonly HandObservation Left = new HandObservation();
        public readonly HandObservation Right = new HandObservation();

        public HandObservation Get(Handedness hand) => hand == Handedness.Left ? Left : Right;

        public void CopyFrom(TrackingFrame other)
        {
            UnityFrame = other.UnityFrame;
            RealtimeSeconds = other.RealtimeSeconds;
            HeadValid = other.HeadValid;
            HeadPosition = other.HeadPosition;
            HeadRotation = other.HeadRotation;
            Left.CopyFrom(other.Left);
            Right.CopyFrom(other.Right);
        }

        public TrackingFrame Clone()
        {
            var copy = new TrackingFrame();
            copy.CopyFrom(this);
            return copy;
        }
    }
}
