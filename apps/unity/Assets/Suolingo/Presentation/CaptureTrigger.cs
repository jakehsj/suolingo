using Suolingo.Capture;
using Suolingo.Tracking;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Suolingo.Presentation
{
    /// <summary>
    /// Milestone 1 explicit-start input without UI interaction packages:
    /// hold an index-thumb pinch for <see cref="holdSeconds"/> on either hand,
    /// or press Space (Editor / Link). Pinching again during countdown cancels.
    /// Placeholder until the learning panel provides a real start button.
    /// </summary>
    public sealed class CaptureTrigger : MonoBehaviour
    {
        [SerializeField] private HandMotionRecorder recorder;
        [SerializeField, Range(0.5f, 1f)] private float pinchThreshold = 0.9f;
        [SerializeField, Min(0.2f)] private float holdSeconds = 1.0f;

        private float _heldFor;
        private bool _requireRelease;

        /// <summary>0..1 progress of the current pinch hold, for UI feedback.</summary>
        public float HoldProgress => Mathf.Clamp01(_heldFor / holdSeconds);

        private void Awake()
        {
            if (recorder == null) recorder = GetComponent<HandMotionRecorder>();
        }

        private void Update()
        {
            if (recorder == null) return;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                Toggle();
                return;
            }
#endif

            var source = recorder.Source;
            var pinching = source != null && source.IsAvailable &&
                           (IsPinching(source.Current.Left) || IsPinching(source.Current.Right));

            if (!pinching)
            {
                _heldFor = 0f;
                _requireRelease = false;
                return;
            }

            if (_requireRelease) return;

            if (recorder.State == RecorderState.Countdown)
            {
                recorder.Cancel();
                _requireRelease = true;
                return;
            }

            if (recorder.State != RecorderState.Idle)
            {
                // A pinch performed as part of the sign must be released before it can start a new capture.
                _heldFor = 0f;
                _requireRelease = true;
                return;
            }

            _heldFor += Time.unscaledDeltaTime;
            if (_heldFor >= holdSeconds)
            {
                recorder.RequestCapture();
                _heldFor = 0f;
                _requireRelease = true;
            }
        }

        private void Toggle()
        {
            if (recorder.State == RecorderState.Idle) recorder.RequestCapture();
            else recorder.Cancel();
        }

        private bool IsPinching(HandObservation hand)
        {
            return hand.Tracked && hand.InputStateValid && !hand.SystemGestureInProgress &&
                   hand.PinchStrength[HandJoints.IndexFinger] >= pinchThreshold;
        }
    }
}
