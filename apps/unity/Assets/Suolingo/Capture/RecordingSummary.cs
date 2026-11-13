using System;
using System.Collections.Generic;
using Suolingo.Tracking;

namespace Suolingo.Capture
{
    /// <summary>
    /// Quality summary of a recording. Informational only: consumers (backend) must
    /// recompute it from frames rather than trusting the stored copy.
    /// </summary>
    public sealed class RecordingSummary
    {
        public int FrameCount;
        public double DurationSeconds;
        public double MeanFrameIntervalSeconds = double.NaN;
        public double MaxFrameIntervalSeconds = double.NaN;
        public HandSummary Left = new HandSummary();
        public HandSummary Right = new HandSummary();

        public HandSummary Get(Handedness hand) => hand == Handedness.Left ? Left : Right;

        public sealed class HandSummary
        {
            public int TrackedFrames;
            public int UntrackedFrames;
            public int HighConfidenceFrames;

            /// <summary>Transitions from tracked to untracked after tracking was first acquired.</summary>
            public int Interruptions;

            /// <summary>Longest contiguous untracked span (seconds), including leading/trailing spans.</summary>
            public double LongestUntrackedSeconds;

            /// <summary>Tracked frames whose SDK sample timestamp equals the previous tracked frame's.</summary>
            public int RepeatedSampleFrames;

            public double TrackedRatio(int frameCount) => frameCount == 0 ? 0 : (double)TrackedFrames / frameCount;
        }

        public static RecordingSummary Compute(IReadOnlyList<TrackingFrame> frames, double captureStartRealtime)
        {
            var s = new RecordingSummary { FrameCount = frames.Count };
            if (frames.Count == 0) return s;

            s.DurationSeconds = frames[frames.Count - 1].RealtimeSeconds - captureStartRealtime;
            if (frames.Count > 1)
            {
                double sum = 0, max = 0;
                for (var i = 1; i < frames.Count; i++)
                {
                    var dt = frames[i].RealtimeSeconds - frames[i - 1].RealtimeSeconds;
                    sum += dt;
                    max = Math.Max(max, dt);
                }
                s.MeanFrameIntervalSeconds = sum / (frames.Count - 1);
                s.MaxFrameIntervalSeconds = max;
            }

            Accumulate(frames, Handedness.Left, s.Left);
            Accumulate(frames, Handedness.Right, s.Right);
            return s;
        }

        private static void Accumulate(IReadOnlyList<TrackingFrame> frames, Handedness hand, HandSummary h)
        {
            var everTracked = false;
            var previousTracked = false;
            var previousSample = double.NaN;
            double untrackedStart = double.NaN;

            for (var i = 0; i < frames.Count; i++)
            {
                var obs = frames[i].Get(hand);
                var time = frames[i].RealtimeSeconds;

                if (obs.Tracked)
                {
                    h.TrackedFrames++;
                    if (obs.HighConfidence) h.HighConfidenceFrames++;
                    if (!double.IsNaN(obs.SampleTime) && obs.SampleTime == previousSample) h.RepeatedSampleFrames++;
                    previousSample = obs.SampleTime;

                    if (!double.IsNaN(untrackedStart))
                    {
                        h.LongestUntrackedSeconds = Math.Max(h.LongestUntrackedSeconds, time - untrackedStart);
                        untrackedStart = double.NaN;
                    }
                    everTracked = true;
                }
                else
                {
                    h.UntrackedFrames++;
                    if (everTracked && previousTracked) h.Interruptions++;
                    if (double.IsNaN(untrackedStart)) untrackedStart = time;
                }

                previousTracked = obs.Tracked;
            }

            if (!double.IsNaN(untrackedStart))
            {
                var end = frames[frames.Count - 1].RealtimeSeconds;
                h.LongestUntrackedSeconds = Math.Max(h.LongestUntrackedSeconds, end - untrackedStart);
            }
        }
    }
}
