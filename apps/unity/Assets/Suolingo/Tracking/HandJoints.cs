namespace Suolingo.Tracking
{
    public enum Handedness
    {
        Left = 0,
        Right = 1,
    }

    /// <summary>
    /// Joint layout used by Suolingo recordings. The order follows the OpenXR
    /// XR_EXT_hand_tracking joint set (26 joints), which is also the layout the
    /// Meta SDK reports when its hand skeleton version is set to OpenXR.
    /// </summary>
    public static class HandJoints
    {
        public const int Count = 26;
        public const int FingerCount = 5;
        public const string LayoutId = "openxr-ext-hand-tracking-26";

        public static readonly string[] Names =
        {
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
        };

        public static readonly string[] FingerNames = { "thumb", "index", "middle", "ring", "little" };

        public const int IndexFinger = 1;
    }
}
