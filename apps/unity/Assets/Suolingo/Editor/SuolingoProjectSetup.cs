using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Suolingo.Capture;
using Suolingo.Presentation;
using Suolingo.Tracking.Meta;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Suolingo.Editor
{
    /// <summary>
    /// Reproducible Quest 3 project configuration and Milestone 1 scene generation.
    /// Every step is idempotent and can run from the menu or headless:
    ///   unity run apps/unity -- -executeMethod Suolingo.Editor.SuolingoProjectSetup.ApplyAllBatch
    /// </summary>
    public static class SuolingoProjectSetup
    {
        public const string ScenePath = "Assets/Suolingo/Scenes/HandTrackingCheck.unity";
        public const string ApplicationId = "dev.suolingo.quest";
        public const string AppVersion = "0.1.0";

        private const string MetaCorePackage = "Packages/com.meta.xr.sdk.core";
        private const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string MetaXRFeatureSetId = "com.meta.openxr.featureset.metaxr";
        private const string XRSettingsFolder = "Assets/XR";

        [MenuItem("Suolingo/Setup/Apply Quest Settings + Rebuild M1 Scene")]
        public static void ApplyAll()
        {
            ApplyProjectSettings();
            CreateHandTrackingCheckScene();
        }

        /// <summary>Batch-mode entry point. Exits with a non-zero code on failure.</summary>
        public static void ApplyAllBatch()
        {
            try
            {
                ApplyAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Suolingo/Setup/Apply Quest Project Settings")]
        public static void ApplyProjectSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            ApplyPlayerSettings();
            ApplyXRManagement();
            ApplyOpenXRFeatures();
            ApplyMetaProjectConfig();
            RunMetaProjectSetupFixes();

            // Re-assert our explicit choices in case automated fixes touched them.
            ApplyPlayerSettings();
            ApplyMetaProjectConfig();

            AssetDatabase.SaveAssets();
            Debug.Log("[Suolingo] Quest project settings applied.");
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Suolingo";
            PlayerSettings.productName = "Suolingo";
            PlayerSettings.bundleVersion = AppVersion;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

            // Development builds talk to a LAN FastAPI server over plain HTTP (see docs/SETUP.md).
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
        }

        private static void ApplyXRManagement()
        {
            if (!AssetDatabase.IsValidFolder(XRSettingsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "XR");
            }

            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                    out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                var existing = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                perTarget = existing.Length > 0
                    ? AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(existing[0]))
                    : null;

                if (perTarget == null)
                {
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perTarget, $"{XRSettingsFolder}/XRGeneralSettingsPerBuildTarget.asset");
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }

            EnsureLoader(perTarget, BuildTargetGroup.Android, assignOpenXR: true);
            // Standalone (Link) is optional for development; keep it configured but uninitialized at start.
            EnsureLoader(perTarget, BuildTargetGroup.Standalone, assignOpenXR: true);
            EditorUtility.SetDirty(perTarget);
        }

        private static void EnsureLoader(XRGeneralSettingsPerBuildTarget perTarget, BuildTargetGroup group, bool assignOpenXR)
        {
            if (!perTarget.HasSettingsForBuildTarget(group))
            {
                perTarget.CreateDefaultSettingsForBuildTarget(group);
            }
            if (!perTarget.HasManagerSettingsForBuildTarget(group))
            {
                perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            }

            var general = perTarget.SettingsForBuildTarget(group);
            general.InitManagerOnStart = true;
            var manager = general.AssignedSettings;
            if (assignOpenXR && !manager.activeLoaders.Any(l => l != null && l.GetType().FullName == OpenXRLoaderType))
            {
                if (!XRPackageMetadataStore.AssignLoader(manager, OpenXRLoaderType, group))
                {
                    throw new InvalidOperationException($"Could not assign OpenXR loader for {group}");
                }
            }
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
        }

        private static void ApplyOpenXRFeatures()
        {
            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                var featureSet = OpenXRFeatureSetManager.GetFeatureSetWithId(group, MetaXRFeatureSetId);
                if (featureSet != null && !featureSet.isEnabled)
                {
                    featureSet.isEnabled = true;
                    OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
                }
            }

            var android = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (android == null)
            {
                throw new InvalidOperationException("OpenXR settings for Android are missing.");
            }

            var questFeature = android.GetFeature<MetaQuestFeature>();
            if (questFeature != null) questFeature.enabled = true;

            var metaFeature = android.GetFeatures().FirstOrDefault(f => f.GetType().FullName == "Meta.XR.MetaXRFeature");
            if (metaFeature == null)
            {
                throw new InvalidOperationException("Meta XR OpenXR feature not found; is com.meta.xr.sdk.core installed?");
            }
            metaFeature.enabled = true;
            EditorUtility.SetDirty(android);
        }

        private static void ApplyMetaProjectConfig()
        {
            var config = OVRProjectConfig.CachedProjectConfig;
            config.targetDeviceTypes = new List<OVRProjectConfig.DeviceType> { OVRProjectConfig.DeviceType.Quest3, OVRProjectConfig.DeviceType.Quest3S };
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
            // Signs contain fast motion; prefer the higher tracking frequency. Revisit if performance suffers.
            config.handTrackingFrequency = OVRProjectConfig.HandTrackingFrequency.HIGH;
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
            config.metaXrFeaturePromptDeclined = false;
            OVRProjectConfig.CommitProjectConfig(config);

            var runtime = OVRRuntimeSettings.GetRuntimeSettings();
            if (runtime.HandSkeletonVersion != OVRHandSkeletonVersion.OpenXR)
            {
                runtime.HandSkeletonVersion = OVRHandSkeletonVersion.OpenXR;
                OVRRuntimeSettings.CommitRuntimeSettings(runtime);
            }
        }

        /// <summary>
        /// Runs Meta's Project Setup Tool fixes synchronously. The public API is async and
        /// needs editor update ticks that batch mode does not provide, so call the internal
        /// blocking variant via reflection. Failing here is non-fatal: the tool still runs
        /// interactively under Meta > Tools > Project Setup Tool.
        /// </summary>
        private static void RunMetaProjectSetupFixes()
        {
            try
            {
                var setupType = Type.GetType("OVRProjectSetup, Oculus.VR.Editor");
                var fixTasks = setupType?.GetMethod("FixTasks", BindingFlags.Static | BindingFlags.NonPublic);
                if (fixTasks == null)
                {
                    Debug.LogWarning("[Suolingo] OVRProjectSetup.FixTasks not found; run the Project Setup Tool manually.");
                    return;
                }

                var parameters = fixTasks.GetParameters();
                var args = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                }
                args[0] = BuildTargetGroup.Android;
                var logParam = Array.FindIndex(parameters, p => p.Name == "logMessages");
                if (logParam >= 0) args[logParam] = Enum.Parse(parameters[logParam].ParameterType, "Changed");
                var blockingParam = Array.FindIndex(parameters, p => p.Name == "blocking");
                if (blockingParam >= 0) args[blockingParam] = true;

                fixTasks.Invoke(null, args);
                Debug.Log("[Suolingo] Meta Project Setup Tool fixes applied for Android.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Suolingo] Meta Project Setup Tool fixes failed: {e}");
            }
        }

        [MenuItem("Suolingo/Setup/Rebuild M1 Hand Tracking Check Scene")]
        public static void CreateHandTrackingCheckScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var rig = InstantiatePackagePrefab("Prefabs/OVRCameraRig.prefab");
            rig.name = "OVRCameraRig";

            var manager = rig.GetComponent<OVRManager>();
            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("isInsightPassthroughEnabled").boolValue = true;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            if (rig.GetComponent<OVRPassthroughLayer>() == null)
            {
                rig.AddComponent<OVRPassthroughLayer>(); // defaults to an underlay
            }

            var centerEye = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
            var camera = centerEye.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent so passthrough shows
            camera.tag = "MainCamera";

            AddHand(rig.transform.Find("TrackingSpace/LeftHandAnchor"), left: true);
            AddHand(rig.transform.Find("TrackingSpace/RightHandAnchor"), left: false);

            var systems = new GameObject("Suolingo");
            var source = systems.AddComponent<MetaHandTrackingSource>();
            var recorder = systems.AddComponent<HandMotionRecorder>();
            var recorderSo = new SerializedObject(recorder);
            recorderSo.FindProperty("trackingSource").objectReferenceValue = source;
            recorderSo.ApplyModifiedPropertiesWithoutUndo();

            var trigger = systems.AddComponent<CaptureTrigger>();
            var triggerSo = new SerializedObject(trigger);
            triggerSo.FindProperty("recorder").objectReferenceValue = recorder;
            triggerSo.ApplyModifiedPropertiesWithoutUndo();

            var panel = systems.AddComponent<TrackingStatusPanel>();
            var panelSo = new SerializedObject(panel);
            panelSo.FindProperty("recorder").objectReferenceValue = recorder;
            panelSo.FindProperty("trigger").objectReferenceValue = trigger;
            panelSo.FindProperty("head").objectReferenceValue = centerEye;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[Suolingo] Scene written to {ScenePath}");
        }

        private static void AddHand(Transform anchor, bool left)
        {
            if (anchor == null) throw new InvalidOperationException("OVRCameraRig hand anchor not found.");

            var hand = InstantiatePackagePrefab("Prefabs/OVRHandPrefab.prefab", anchor);
            hand.name = left ? "LeftHand" : "RightHand";
            // Unpack so hand side / skeleton type are stored in the scene itself. As a prefab instance,
            // a value equal to the (OnValidate-modified, in-memory) prefab asset is not saved as an override.
            PrefabUtility.UnpackPrefabInstance(hand, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var ovrHand = hand.GetComponent<OVRHand>();
            var skeleton = hand.GetComponent<OVRSkeleton>();
            var mesh = hand.GetComponent<OVRMesh>();
            var skeletonType = left ? OVRSkeleton.SkeletonType.XRHandLeft : OVRSkeleton.SkeletonType.XRHandRight;
            var meshType = left ? OVRMesh.MeshType.XRHandLeft : OVRMesh.MeshType.XRHandRight;

            SetEnum(ovrHand, "HandType", (int)(left ? OVRHand.Hand.HandLeft : OVRHand.Hand.HandRight));
            // OVRHand.OnValidate re-syncs skeleton/mesh types from HandType; run it first so the
            // explicit values below are final.
            ovrHand.OnValidate();
            SetEnum(skeleton, "_skeletonType", (int)skeletonType);
            SetEnum(mesh, "_meshType", (int)meshType);

            var actualSkeleton = GetInt(skeleton, "_skeletonType");
            var actualMesh = GetInt(mesh, "_meshType");
            if (actualSkeleton != (int)skeletonType || actualMesh != (int)meshType)
            {
                throw new InvalidOperationException(
                    $"{hand.name}: expected skeleton/mesh {skeletonType}/{meshType}, got {actualSkeleton}/{actualMesh}");
            }
        }

        private static int GetInt(UnityEngine.Object target, string property) =>
            new SerializedObject(target).FindProperty(property).intValue;

        private static void SetEnum(UnityEngine.Object target, string property, int value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogWarning($"[Suolingo] {target.GetType().Name}.{property} not found");
                return;
            }
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject InstantiatePackagePrefab(string relativePath, Transform parent = null)
        {
            var path = $"{MetaCorePackage}/{relativePath}";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException($"Prefab not found: {path}");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (parent == null) SceneManager.MoveGameObjectToScene(instance, SceneManager.GetActiveScene());
            return instance;
        }
    }
}
