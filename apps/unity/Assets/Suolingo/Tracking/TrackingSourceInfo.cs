using System;

namespace Suolingo.Tracking
{
    /// <summary>
    /// Describes where tracking data came from and how to interpret its coordinates.
    /// Written into every recording so files stay interpretable after SDK changes.
    /// </summary>
    [Serializable]
    public sealed class TrackingSourceInfo
    {
        /// <summary>Stable identifier of the source implementation, e.g. "meta-ovrplugin".</summary>
        public string Provider = "unknown";

        /// <summary>Version of the SDK wrapper compiled into the app.</summary>
        public string SdkWrapperVersion = "unknown";

        /// <summary>Version reported by the native runtime/plugin at run time.</summary>
        public string RuntimeVersion = "unknown";

        /// <summary>Joint layout identifier; see <see cref="HandJoints.LayoutId"/>.</summary>
        public string JointLayout = HandJoints.LayoutId;

        /// <summary>Tracking origin mode reported by the SDK (e.g. FloorLevel, Stage, EyeLevel).</summary>
        public string TrackingOrigin = "unknown";

        /// <summary>Human-readable description of the coordinate space used for all poses.</summary>
        public string Space = "tracking-space";

        /// <summary>Display refresh rate reported by the runtime (Hz); NaN when unknown.</summary>
        public float DisplayRefreshRateHz = float.NaN;

        public TrackingSourceInfo Clone() => (TrackingSourceInfo)MemberwiseClone();
    }
}
