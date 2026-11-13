using System;
using System.IO;
using Suolingo.Capture;
using Suolingo.Tracking;
using UnityEditor;
using UnityEngine;

namespace Suolingo.Editor
{
    /// <summary>
    /// Writes a synthetic recording with the real Unity JSON writer so the Python contract
    /// tests can validate Unity's actual output (tests/fixtures/recordings/unity-writer-synthetic.json).
    /// Synthetic numbers only — not tracking data.
    ///   unity run apps/unity -- -executeMethod Suolingo.Editor.ContractSampleWriter.WriteBatch
    /// </summary>
    public static class ContractSampleWriter
    {
        public const string OutputPath = "../../tests/fixtures/recordings/unity-writer-synthetic.json";

        [MenuItem("Suolingo/Dev/Write Contract Sample (synthetic)")]
        public static void Write()
        {
            var recording = new HandRecording
            {
                RecordingId = "unity-writer-synthetic-0001",
                CreatedAtUtc = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc),
                ParticipantId = "synthetic",
                Notes = "Synthetic sample written by ContractSampleWriter. Not real tracking data.",
                CountdownSeconds = 3f,
                RequestedDurationSeconds = 0.25f,
                CaptureStartRealtime = 100.0,
                DisplayRefreshRateHz = 72f,
                Producer = new ProducerInfo
                {
                    App = "suolingo-contract-sample",
                    AppVersion = SuolingoProjectSetup.AppVersion,
                    UnityVersion = Application.unityVersion,
                    Platform = "synthetic",
                    DeviceModel = "synthetic",
                },
                Source = new TrackingSourceInfo
                {
                    Provider = "synthetic",
                    SdkWrapperVersion = "n/a",
                    RuntimeVersion = "n/a",
                    TrackingOrigin = "Stage",
                    Space = "synthetic tracking space",
                },
            };

            for (var i = 0; i < 19; i++)
            {
                var f = new TrackingFrame
                {
                    UnityFrame = 1000 + i,
                    RealtimeSeconds = 100.0 + i / 72.0,
                    HeadValid = i != 3,
                    HeadPosition = new Vector3(0f, 1.6f, 0f),
                    HeadRotation = Quaternion.Euler(5f, 0f, 0f),
                };
                FillHand(f.Left, i, tracked: i < 6 || i > 10, x: -0.15f);
                FillHand(f.Right, i, tracked: true, x: 0.15f);
                if (i == 9) f.Right.SampleTime = 500.0 + 8 / 72.0; // SDK delivered no new sample this frame
                recording.Frames.Add(f);
            }

            var json = HandRecordingJsonWriter.Write(recording);
            var path = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json + "\n");
            Debug.Log($"[Suolingo] Contract sample written to {path}");
        }

        public static void WriteBatch()
        {
            try
            {
                Write();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void FillHand(HandObservation h, int i, bool tracked, float x)
        {
            h.Clear();
            h.Tracked = tracked;
            h.HighConfidence = tracked && i % 5 != 0;
            h.InputStateValid = tracked;
            h.SampleTime = 500.0 + i / 72.0;
            h.FingerHighConfidence[0] = tracked;
            h.PinchStrength[1] = tracked ? 0.25f : 0f;
            if (!tracked) return;
            h.HandScale = 1.02f;
            h.RootPosition = new Vector3(x, 1.2f, 0.35f);
            h.RootRotation = Quaternion.Euler(0f, 0f, 90f);
            for (var j = 0; j < HandJoints.Count; j++)
            {
                h.JointPositions[j] = new Vector3(x + 0.004f * j, 1.2f + 0.01f * Mathf.Sin(i * 0.3f), 0.35f);
                h.JointRotations[j] = Quaternion.Euler(0f, 3f * j, 0f);
            }
        }
    }
}
