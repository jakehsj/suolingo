using System;

namespace Suolingo.Tracking
{
    /// <summary>
    /// Boundary between SDK-specific tracking and the rest of the app. Implementations
    /// report measured tracking data (not poses modified for interaction visuals) and
    /// must mark samples without usable data as untracked instead of repeating stale poses.
    /// </summary>
    public interface IHandTrackingSource
    {
        TrackingSourceInfo Info { get; }

        /// <summary>True when the source can currently deliver hand tracking data at all.</summary>
        bool IsAvailable { get; }

        /// <summary>Human-readable reason when <see cref="IsAvailable"/> is false; otherwise null.</summary>
        string UnavailableReason { get; }

        /// <summary>The latest sample. Owned by the source; copy it if you need to keep it.</summary>
        TrackingFrame Current { get; }

        /// <summary>Raised once per sampled frame, after <see cref="Current"/> is updated.</summary>
        event Action<TrackingFrame> FrameSampled;
    }
}
