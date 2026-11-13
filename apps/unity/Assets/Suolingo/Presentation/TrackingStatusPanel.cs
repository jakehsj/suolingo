using System.IO;
using System.Text;
using Suolingo.Capture;
using Suolingo.Tracking;
using UnityEngine;
using UnityEngine.UI;

namespace Suolingo.Presentation
{
    /// <summary>
    /// World-space diagnostic panel: per-hand tracking status, session interruption
    /// counts and recorder state. Builds its own canvas so the scene needs no UI assets.
    /// </summary>
    public sealed class TrackingStatusPanel : MonoBehaviour
    {
        [SerializeField] private HandMotionRecorder recorder;
        [SerializeField] private CaptureTrigger trigger;

        [Tooltip("Head/camera transform used to place the panel. Defaults to Camera.main.")]
        [SerializeField] private Transform head;

        [SerializeField] private float distance = 0.7f;
        [SerializeField] private float heightOffset = -0.15f;
        [SerializeField] private float refreshInterval = 0.1f;

        private Text _text;
        private Transform _panel;
        private bool _placed;
        private float _nextRefresh;
        private readonly StringBuilder _sb = new StringBuilder(1024);

        private readonly HandSessionStats _left = new HandSessionStats();
        private readonly HandSessionStats _right = new HandSessionStats();

        private sealed class HandSessionStats
        {
            public bool WasTracked;
            public bool EverTracked;
            public int Interruptions;
            public float LostSince = -1f;
        }

        private void Awake()
        {
            if (recorder == null) recorder = GetComponent<HandMotionRecorder>();
            if (trigger == null) trigger = GetComponent<CaptureTrigger>();
            BuildCanvas();
        }

        private void Update()
        {
            var source = recorder != null ? recorder.Source : null;
            if (source != null && source.IsAvailable)
            {
                Track(source.Current.Left, _left);
                Track(source.Current.Right, _right);
            }

            PlaceIfNeeded();

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            _text.text = Compose(source);
        }

        private static void Track(HandObservation obs, HandSessionStats stats)
        {
            if (obs.Tracked)
            {
                stats.EverTracked = true;
                stats.LostSince = -1f;
            }
            else
            {
                if (stats.WasTracked) stats.Interruptions++;
                if (stats.LostSince < 0f) stats.LostSince = Time.unscaledTime;
            }
            stats.WasTracked = obs.Tracked;
        }

        private string Compose(IHandTrackingSource source)
        {
            _sb.Clear();
            _sb.Append("<b>Suolingo · M1 hand tracking check</b>\n");

            if (source == null)
            {
                _sb.Append("<color=#ff6666>No tracking source</color>\n");
                return _sb.ToString();
            }

            if (!source.IsAvailable)
            {
                _sb.Append("<color=#ffaa33>Tracking unavailable: ").Append(source.UnavailableReason).Append("</color>\n");
            }
            else
            {
                AppendHand("LEFT ", source.Current.Left, _left);
                AppendHand("RIGHT", source.Current.Right, _right);
            }

            _sb.Append("Head: ").Append(source.Current.HeadValid ? "tracked" : "<color=#ff6666>not tracked</color>")
               .Append("   origin: ").Append(source.Info.TrackingOrigin).Append('\n');

            _sb.Append('\n');
            AppendRecorder();
            return _sb.ToString();
        }

        private void AppendHand(string label, HandObservation obs, HandSessionStats stats)
        {
            _sb.Append(label).Append("  ");
            if (obs.Tracked)
            {
                _sb.Append(obs.HighConfidence
                    ? "<color=#66ff88>TRACKED · high</color>"
                    : "<color=#ffdd55>TRACKED · low conf</color>");
            }
            else
            {
                var lostFor = stats.LostSince >= 0f ? Time.unscaledTime - stats.LostSince : 0f;
                _sb.Append("<color=#ff6666>LOST</color> ").Append(lostFor.ToString("0.0")).Append("s (no pose)");
            }
            _sb.Append("   interruptions: ").Append(stats.Interruptions).Append('\n');
        }

        private void AppendRecorder()
        {
            if (recorder == null) return;
            switch (recorder.State)
            {
                case RecorderState.Idle:
                    _sb.Append("Recorder: idle — hold index pinch 1s (or Space) to record ")
                       .Append(recorder.CaptureSeconds.ToString("0.#")).Append("s");
                    if (trigger != null && trigger.HoldProgress > 0f)
                    {
                        _sb.Append("  [").Append(Mathf.RoundToInt(trigger.HoldProgress * 100)).Append("%]");
                    }
                    _sb.Append('\n');
                    break;
                case RecorderState.Countdown:
                    _sb.Append("<size=48><color=#ffdd55>Get ready… ")
                       .Append(Mathf.CeilToInt(recorder.PhaseRemaining)).Append("</color></size>  (pinch to cancel)\n");
                    break;
                case RecorderState.Capturing:
                    _sb.Append("<size=48><color=#ff4444>● REC ")
                       .Append(recorder.PhaseRemaining.ToString("0.0")).Append("s</color></size>  frames: ")
                       .Append(recorder.CapturedFrameCount).Append('\n');
                    break;
                case RecorderState.Saving:
                    _sb.Append("Saving…\n");
                    break;
            }

            var summary = recorder.LastSummary;
            if (summary != null)
            {
                _sb.Append("Last: ").Append(summary.FrameCount).Append(" frames, ")
                   .Append(summary.DurationSeconds.ToString("0.00")).Append("s | L ")
                   .Append((summary.Left.TrackedRatio(summary.FrameCount) * 100).ToString("0")).Append("% (")
                   .Append(summary.Left.Interruptions).Append(" gaps) R ")
                   .Append((summary.Right.TrackedRatio(summary.FrameCount) * 100).ToString("0")).Append("% (")
                   .Append(summary.Right.Interruptions).Append(" gaps)\n");
            }
            if (!string.IsNullOrEmpty(recorder.LastSavedPath))
            {
                _sb.Append("File: ").Append(Path.GetFileName(recorder.LastSavedPath)).Append('\n');
            }
            else if (recorder.LastRecording != null && !recorder.ExportToFile)
            {
                _sb.Append("File export disabled (kept in memory only)\n");
            }
            if (!string.IsNullOrEmpty(recorder.LastError))
            {
                _sb.Append("<color=#ff6666>").Append(recorder.LastError).Append("</color>\n");
            }
        }

        private void PlaceIfNeeded()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;

            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) return;
            forward.Normalize();

            var target = head.position + forward * distance + Vector3.up * heightOffset;
            var tooFar = _placed && Vector3.Distance(_panel.position, target) > 0.8f;
            if (_placed && !tooFar) return;

            // Avoid placing at the origin before the headset reports a pose.
            if (!_placed && head.position == Vector3.zero && head.rotation == Quaternion.identity) return;

            _panel.position = target;
            _panel.rotation = Quaternion.LookRotation(forward, Vector3.up);
            _placed = true;
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("TrackingStatusCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _panel = canvasGo.transform;
            _panel.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f;

            var rect = (RectTransform)_panel;
            rect.sizeDelta = new Vector2(900, 420);
            rect.localScale = Vector3.one * 0.0006f; // 900px ≈ 0.54 m wide

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_panel, false);
            var bgRect = (RectTransform)bg.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.82f);

            var textGo = new GameObject("Status", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(_panel, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24, 18);
            textRect.offsetMax = new Vector2(-24, -18);

            _text = textGo.GetComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 30;
            _text.supportRichText = true;
            _text.color = Color.white;
            _text.alignment = TextAnchor.UpperLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.text = "Starting…";
        }
    }
}
