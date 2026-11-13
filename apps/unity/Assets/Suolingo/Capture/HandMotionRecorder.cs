using System;
using System.IO;
using System.Threading.Tasks;
using Suolingo.Tracking;
using UnityEngine;

namespace Suolingo.Capture
{
    public enum RecorderState
    {
        Idle,
        Countdown,
        Capturing,
        Saving,
    }

    /// <summary>
    /// Explicit-start capture: countdown, then a bounded capture interval of tracking
    /// frames. Optionally exports the attempt as JSON (development use only).
    /// </summary>
    public sealed class HandMotionRecorder : MonoBehaviour
    {
        [Tooltip("Component implementing IHandTrackingSource.")]
        [SerializeField] private MonoBehaviour trackingSource;

        [SerializeField, Min(0f)] private float countdownSeconds = 3f;
        [SerializeField, Min(0.5f)] private float captureSeconds = 4f;

        [Tooltip("Write each completed attempt to <persistentDataPath>/recordings as JSON.")]
        [SerializeField] private bool exportToFile = true;

        [Tooltip("Pseudonymous participant ID written into exported files (never a real name).")]
        [SerializeField] private string participantId = "";

        [Tooltip("Optional expression ID for the attempt; empty for free capture.")]
        [SerializeField] private string expressionId = "";

        private IHandTrackingSource _source;
        private HandRecording _current;
        private double _stateStartedAt;

        public RecorderState State { get; private set; } = RecorderState.Idle;
        public float CountdownSeconds => countdownSeconds;
        public float CaptureSeconds => captureSeconds;
        public bool ExportToFile { get => exportToFile; set => exportToFile = value; }
        public string ParticipantId { get => participantId; set => participantId = value; }
        public string ExpressionId { get => expressionId; set => expressionId = value; }

        public HandRecording LastRecording { get; private set; }
        public RecordingSummary LastSummary { get; private set; }
        public string LastSavedPath { get; private set; }
        public string LastError { get; private set; }

        public static string RecordingsDirectory => Path.Combine(Application.persistentDataPath, "recordings");

        public event Action<RecorderState> StateChanged;
        public event Action<HandRecording, RecordingSummary> RecordingCompleted;

        public IHandTrackingSource Source => _source;

        /// <summary>Seconds remaining in the current countdown or capture phase.</summary>
        public float PhaseRemaining
        {
            get
            {
                var elapsed = (float)(Time.realtimeSinceStartupAsDouble - _stateStartedAt);
                return State switch
                {
                    RecorderState.Countdown => Mathf.Max(0f, countdownSeconds - elapsed),
                    RecorderState.Capturing => Mathf.Max(0f, captureSeconds - elapsed),
                    _ => 0f,
                };
            }
        }

        public int CapturedFrameCount => _current?.Frames.Count ?? 0;

        private void Awake()
        {
            _source = trackingSource as IHandTrackingSource;
            if (_source == null)
            {
                _source = GetComponent<IHandTrackingSource>();
            }
            if (_source == null)
            {
                Debug.LogError("[Suolingo] HandMotionRecorder needs a component implementing IHandTrackingSource.", this);
                enabled = false;
            }
        }

        public void SetSource(IHandTrackingSource source) => _source = source;

        /// <summary>Begins the countdown. Ignored unless idle.</summary>
        public bool RequestCapture()
        {
            if (State != RecorderState.Idle || _source == null) return false;
            LastError = null;
            SetState(RecorderState.Countdown);
            return true;
        }

        /// <summary>Aborts a countdown or capture without saving.</summary>
        public void Cancel()
        {
            if (State == RecorderState.Countdown || State == RecorderState.Capturing)
            {
                _current = null;
                SetState(RecorderState.Idle);
            }
        }

        private void Update()
        {
            if (State == RecorderState.Countdown && PhaseRemaining <= 0f)
            {
                BeginCapture();
            }
        }

        // Sample in LateUpdate so the tracking source (execution order -100, Update) has run.
        private void LateUpdate()
        {
            if (State != RecorderState.Capturing) return;

            var frame = _source.Current;
            var frames = _current.Frames;
            if (frames.Count == 0 || frames[frames.Count - 1].UnityFrame != frame.UnityFrame)
            {
                frames.Add(frame.Clone());
            }

            if (Time.realtimeSinceStartupAsDouble - _current.CaptureStartRealtime >= captureSeconds)
            {
                _ = FinishCaptureAsync();
            }
        }

        private void BeginCapture()
        {
            _current = new HandRecording
            {
                ParticipantId = string.IsNullOrWhiteSpace(participantId) ? null : participantId.Trim(),
                ExpressionId = string.IsNullOrWhiteSpace(expressionId) ? null : expressionId.Trim(),
                CountdownSeconds = countdownSeconds,
                RequestedDurationSeconds = captureSeconds,
                CaptureStartRealtime = Time.realtimeSinceStartupAsDouble,
                Producer = new ProducerInfo
                {
                    AppVersion = Application.version,
                    UnityVersion = Application.unityVersion,
                    Platform = Application.platform.ToString(),
                    DeviceModel = SystemInfo.deviceModel,
                },
            };
            SetState(RecorderState.Capturing);
            _stateStartedAt = _current.CaptureStartRealtime;
        }

        private async Task FinishCaptureAsync()
        {
            var recording = _current;
            _current = null;
            recording.Source = _source.Info.Clone();
            recording.DisplayRefreshRateHz = float.IsNaN(recording.Source.DisplayRefreshRateHz)
                ? ReadRefreshRate()
                : recording.Source.DisplayRefreshRateHz;
            SetState(RecorderState.Saving);

            var summary = RecordingSummary.Compute(recording.Frames, recording.CaptureStartRealtime);
            LastRecording = recording;
            LastSummary = summary;
            LastSavedPath = null;

            if (exportToFile)
            {
                try
                {
                    var json = HandRecordingJsonWriter.Write(recording, summary);
                    var dir = RecordingsDirectory;
                    var fileName = $"{recording.CreatedAtUtc:yyyyMMdd'T'HHmmss'Z'}_{recording.RecordingId.Substring(0, 8)}.json";
                    var path = Path.Combine(dir, fileName);
                    await Task.Run(() =>
                    {
                        Directory.CreateDirectory(dir);
                        File.WriteAllText(path, json);
                    });
                    LastSavedPath = path;
                    Debug.Log($"[Suolingo] Saved recording ({summary.FrameCount} frames) to {path}");
                }
                catch (Exception e)
                {
                    LastError = $"Export failed: {e.Message}";
                    Debug.LogException(e);
                }
            }

            RecordingCompleted?.Invoke(recording, summary);
            SetState(RecorderState.Idle);
        }

        private void SetState(RecorderState state)
        {
            State = state;
            _stateStartedAt = Time.realtimeSinceStartupAsDouble;
            StateChanged?.Invoke(state);
        }

        private static float ReadRefreshRate()
        {
            var rate = Screen.currentResolution.refreshRateRatio.value;
            return rate > 0 ? (float)rate : float.NaN;
        }
    }
}
