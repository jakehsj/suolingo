using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Suolingo.Capture;
using Suolingo.Tracking;
using UnityEngine;

namespace Suolingo.Tests
{
    public sealed class RecordingTests
    {
        private static TrackingFrame Frame(int index, double time, bool leftTracked, bool rightTracked, double sampleTime)
        {
            var f = new TrackingFrame { UnityFrame = index, RealtimeSeconds = time, HeadValid = true };
            f.HeadPosition = new Vector3(0f, 1.6f, 0f);
            Fill(f.Left, leftTracked, sampleTime);
            Fill(f.Right, rightTracked, sampleTime);
            return f;
        }

        private static void Fill(HandObservation h, bool tracked, double sampleTime)
        {
            h.Clear();
            h.Tracked = tracked;
            h.HighConfidence = tracked;
            h.SampleTime = sampleTime;
            if (!tracked) return;
            h.HandScale = 1f;
            for (var i = 0; i < HandJoints.Count; i++)
            {
                h.JointPositions[i] = new Vector3(0.01f * i, 1.2f, 0.3f);
                h.JointRotations[i] = Quaternion.identity;
            }
        }

        private static HandRecording Sample()
        {
            var r = new HandRecording { CaptureStartRealtime = 10.0, RequestedDurationSeconds = 0.1f, CountdownSeconds = 3f };
            // Left: tracked, lost for two frames, tracked again. Right: tracked throughout.
            r.Frames.Add(Frame(1, 10.000, true, true, 5.000));
            r.Frames.Add(Frame(2, 10.014, false, true, 5.014));
            r.Frames.Add(Frame(3, 10.028, false, true, 5.028));
            r.Frames.Add(Frame(4, 10.042, true, true, 5.028)); // repeated SDK sample
            return r;
        }

        [Test]
        public void Summary_CountsInterruptionsAndGaps()
        {
            var r = Sample();
            var s = RecordingSummary.Compute(r.Frames, r.CaptureStartRealtime);

            Assert.AreEqual(4, s.FrameCount);
            Assert.AreEqual(2, s.Left.TrackedFrames);
            Assert.AreEqual(2, s.Left.UntrackedFrames);
            Assert.AreEqual(1, s.Left.Interruptions);
            Assert.AreEqual(0.028, s.Left.LongestUntrackedSeconds, 1e-9);
            Assert.AreEqual(0, s.Right.Interruptions);
            Assert.AreEqual(1, s.Right.RepeatedSampleFrames);
            Assert.AreEqual(0.042, s.DurationSeconds, 1e-9);
        }

        [Test]
        public void LeadingUntrackedFramesAreNotInterruptions()
        {
            var r = new HandRecording { CaptureStartRealtime = 0 };
            r.Frames.Add(Frame(1, 0.0, false, false, double.NaN));
            r.Frames.Add(Frame(2, 0.1, true, false, 1.0));
            var s = RecordingSummary.Compute(r.Frames, 0);
            Assert.AreEqual(0, s.Left.Interruptions);
            Assert.AreEqual(0.1, s.Left.LongestUntrackedSeconds, 1e-9);
            Assert.AreEqual(0.1, s.Right.LongestUntrackedSeconds, 1e-9);
        }

        [Test]
        public void Json_UntrackedHandHasNoPose()
        {
            var json = HandRecordingJsonWriter.Write(Sample());
            StringAssert.Contains("\"schema\":\"suolingo.hand-recording\"", json);
            StringAssert.Contains("\"tracked\":false", json);
            StringAssert.Contains("\"root\":null,\"joint_positions\":null,\"joint_rotations\":null", json);
            StringAssert.Contains("\"participant_id\":null", json);
            StringAssert.DoesNotContain("NaN", json);
        }

        [Test]
        public void Json_IsCultureInvariant()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var json = HandRecordingJsonWriter.Write(Sample());
                StringAssert.Contains("\"t\":0.014", json);
                StringAssert.DoesNotContain("0,014", json);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void Json_EscapesStrings()
        {
            var r = Sample();
            r.Notes = "line1\n\"quoted\"";
            var json = HandRecordingJsonWriter.Write(r);
            StringAssert.Contains("\"notes\":\"line1\\n\\\"quoted\\\"\"", json);
        }

        [Test]
        public void JointNamesMatchCount()
        {
            Assert.AreEqual(HandJoints.Count, HandJoints.Names.Length);
            Assert.AreEqual(HandJoints.FingerCount, HandJoints.FingerNames.Length);
        }
    }
}
