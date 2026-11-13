using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Suolingo.Editor
{
    /// <summary>
    /// Quest APK build. Headless:
    ///   unity run apps/unity -- -executeMethod Suolingo.Editor.SuolingoBuild.BuildQuestApkBatch
    /// Output: apps/unity/Builds/Android/suolingo.apk (git-ignored).
    /// </summary>
    public static class SuolingoBuild
    {
        public const string OutputPath = "Builds/Android/suolingo.apk";

        [MenuItem("Suolingo/Build/Build Quest APK (Development)")]
        public static void BuildQuestApkMenu()
        {
            var report = BuildQuestApk(development: true);
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(OutputPath);
            }
        }

        public static void BuildQuestApkBatch()
        {
            try
            {
                var development = !Array.Exists(Environment.GetCommandLineArgs(), a => a == "-suolingoRelease");
                var report = BuildQuestApk(development);
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static BuildReport BuildQuestApk(bool development)
        {
            SuolingoProjectSetup.ApplyProjectSettings();
            if (!File.Exists(SuolingoProjectSetup.ScenePath))
            {
                SuolingoProjectSetup.CreateHandTrackingCheckScene();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SuolingoProjectSetup.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var sizeMb = File.Exists(OutputPath) ? new FileInfo(OutputPath).Length / (1024f * 1024f) : 0f;
            Debug.Log($"[Suolingo] Build {report.summary.result}: {OutputPath} ({sizeMb:0.0} MB, {report.summary.totalTime})");
            return report;
        }
    }
}
