using System;
using System.Collections.Generic;
using Suolingo.Tracking;

namespace Suolingo.Capture
{
    /// <summary>A bounded capture of tracking frames plus the metadata needed to interpret them.</summary>
    public sealed class HandRecording
    {
        public const string SchemaId = "suolingo.hand-recording";
        public const string SchemaVersion = "0.1.0";

        public string RecordingId = Guid.NewGuid().ToString("N");
        public DateTime CreatedAtUtc = DateTime.UtcNow;

        /// <summary>Pseudonymous participant ID (never a name). Null when not set.</summary>
        public string ParticipantId;

        /// <summary>Expression the attempt was meant to perform. Null for free capture.</summary>
        public string ExpressionId;

        public string Notes;

        public ProducerInfo Producer = new ProducerInfo();
        public TrackingSourceInfo Source = new TrackingSourceInfo();

        public float CountdownSeconds;
        public float RequestedDurationSeconds;

        /// <summary>Realtime (seconds since app start) when capture began; frame t values are relative to this.</summary>
        public double CaptureStartRealtime;

        public float DisplayRefreshRateHz = float.NaN;

        /// <summary>Why the capture ended: "completed" or "cancelled".</summary>
        public string EndReason = "completed";

        public readonly List<TrackingFrame> Frames = new List<TrackingFrame>();
    }

    [Serializable]
    public sealed class ProducerInfo
    {
        public string App = "suolingo-quest";
        public string AppVersion = "unknown";
        public string UnityVersion = "unknown";
        public string Platform = "unknown";
        public string DeviceModel = "unknown";
    }
}
