using System;
using UnityEngine;

namespace Suolingo.Tracking.Meta
{
    /// <summary>
    /// Reads raw hand tracking from the Meta SDK (OVRPlugin) once per frame.
    ///
    /// Uses <c>OVRPlugin.GetHandState</c> directly rather than OVRSkeleton/SyntheticHand
    /// transforms so recorded data is the measured pose, not one adjusted for visuals.
    /// Poses are converted to Unity's left-handed convention and expressed in the
    /// OVRCameraRig tracking space (the parent of the hand anchors).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class MetaHandTrackingSource : MonoBehaviour, IHandTrackingSource
    {
        public const string ProviderId = "meta-ovrplugin";

        private readonly TrackingFrame _frame = new TrackingFrame();
        private readonly TrackingSourceInfo _info = new TrackingSourceInfo();
        private OVRPlugin.HandState _leftState = new OVRPlugin.HandState();
        private OVRPlugin.HandState _rightState = new OVRPlugin.HandState();
        private bool _warnedSkeleton;

        public TrackingSourceInfo Info => _info;
        public bool IsAvailable { get; private set; }
        public string UnavailableReason { get; private set; } = "Not sampled yet";
        public TrackingFrame Current => _frame;
        public event Action<TrackingFrame> FrameSampled;

        private void Awake()
        {
            _info.Provider = ProviderId;
            _info.SdkWrapperVersion = OVRPlugin.wrapperVersion?.ToString() ?? "unknown";
            _info.JointLayout = HandJoints.LayoutId;
            _info.Space = "OVRCameraRig tracking space (Unity left-handed, meters, +Y up, +Z forward)";
        }

        private void Update()
        {
            _info.RuntimeVersion = OVRPlugin.version?.ToString() ?? "unknown";
            _info.TrackingOrigin = OVRManager.instance != null
                ? OVRManager.instance.trackingOriginType.ToString()
                : "unknown";

            var refresh = OVRPlugin.systemDisplayFrequency;
            _info.DisplayRefreshRateHz = refresh > 0 ? refresh : float.NaN;

            _frame.UnityFrame = Time.frameCount;
            _frame.RealtimeSeconds = Time.realtimeSinceStartupAsDouble;

            SampleHead();

            var skeletonOk = OVRPlugin.HandSkeletonVersion == OVRHandSkeletonVersion.OpenXR;
            var handTrackingEnabled = OVRPlugin.GetHandTrackingEnabled();

            if (!skeletonOk)
            {
                SetUnavailable($"Hand skeleton version is {OVRPlugin.HandSkeletonVersion}; Suolingo requires OpenXR (26 joints)");
                if (!_warnedSkeleton)
                {
                    Debug.LogError($"[Suolingo] {UnavailableReason}. Set it in OVRRuntimeSettings.");
                    _warnedSkeleton = true;
                }
            }
            else if (!handTrackingEnabled)
            {
                SetUnavailable("Hand tracking is not enabled on the device (controllers in use or permission off)");
            }
            else
            {
                IsAvailable = true;
                UnavailableReason = null;
                SampleHand(OVRPlugin.Hand.HandLeft, ref _leftState, _frame.Left);
                SampleHand(OVRPlugin.Hand.HandRight, ref _rightState, _frame.Right);
            }

            FrameSampled?.Invoke(_frame);
        }

        private void SetUnavailable(string reason)
        {
            IsAvailable = false;
            UnavailableReason = reason;
            _frame.Left.Clear();
            _frame.Right.Clear();
        }

        private void SampleHead()
        {
            var positionTracked = OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.Head);
            var orientationTracked = OVRPlugin.GetNodeOrientationTracked(OVRPlugin.Node.Head);
            _frame.HeadValid = positionTracked && orientationTracked;
            if (!_frame.HeadValid) return;

            var pose = OVRPlugin.GetNodePose(OVRPlugin.Node.Head, OVRPlugin.Step.Render).ToOVRPose();
            _frame.HeadPosition = pose.position;
            _frame.HeadRotation = pose.orientation;
        }

        private static void SampleHand(OVRPlugin.Hand hand, ref OVRPlugin.HandState state, HandObservation target)
        {
            if (!OVRPlugin.GetHandState(OVRPlugin.Step.Render, hand, ref state))
            {
                target.Clear();
                return;
            }

            var tracked = (state.Status & OVRPlugin.HandStatus.HandTracked) != 0;
            var jointCount = state.BonePositions?.Length ?? 0;
            if (jointCount != HandJoints.Count || (state.BoneRotations?.Length ?? 0) != HandJoints.Count)
            {
                // Unexpected layout: never emit joints we cannot map.
                target.Clear();
                return;
            }

            target.Tracked = tracked;
            target.InputStateValid = (state.Status & OVRPlugin.HandStatus.InputStateValid) != 0;
            target.SystemGestureInProgress = (state.Status & OVRPlugin.HandStatus.SystemGestureInProgress) != 0;
            target.HighConfidence = state.HandConfidence == OVRPlugin.TrackingConfidence.High;
            target.SampleTime = state.SampleTimeStamp > 0 ? state.SampleTimeStamp : double.NaN;
            target.HandScale = state.HandScale > 0 ? state.HandScale : float.NaN;

            for (var f = 0; f < HandJoints.FingerCount; f++)
            {
                target.PinchStrength[f] = state.PinchStrength != null && f < state.PinchStrength.Length
                    ? state.PinchStrength[f]
                    : 0f;
                target.FingerHighConfidence[f] = state.FingerConfidences != null && f < state.FingerConfidences.Length
                    && state.FingerConfidences[f] == OVRPlugin.TrackingConfidence.High;
            }

            if (!tracked)
            {
                // Keep flags, drop pose data so stale poses can never be mistaken for motion.
                target.RootPosition = Vector3.zero;
                target.RootRotation = Quaternion.identity;
                Array.Clear(target.JointPositions, 0, HandJoints.Count);
                for (var i = 0; i < HandJoints.Count; i++) target.JointRotations[i] = Quaternion.identity;
                return;
            }

            target.RootPosition = state.RootPose.Position.FromFlippedZVector3f();
            target.RootRotation = state.RootPose.Orientation.FromFlippedZQuatf();
            for (var i = 0; i < HandJoints.Count; i++)
            {
                target.JointPositions[i] = state.BonePositions[i].FromFlippedZVector3f();
                target.JointRotations[i] = state.BoneRotations[i].FromFlippedZQuatf();
            }
        }
    }
}
