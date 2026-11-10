#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IslandBuilder.EditorTools
{
    public static class Phase1ProjectConfigurator
    {
        private const string UrpAssetPath = "Assets/Settings/Island_URP_Asset.asset";
        private const string UrpRendererPath = "Assets/Settings/Island_URP_Renderer.asset";

        [MenuItem("IslandBuilder/Phase 1/1. Enforce Player & URP Settings", false, 1)]
        public static void EnforcePhase1Settings()
        {
            EnsureSettingsDirectory();
            ConfigureAndroidPlayerSettings();
            ConfigureUrpPipelineAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase 1 Configurator] Successfully enforced Meta Quest Vulkan + URP Forward settings.");
        }

        [MenuItem("IslandBuilder/Phase 1/2. Audit Phase 1 Compliance", false, 2)]
        public static void AuditPhase1Settings()
        {
            int errors = 0;

            // 1. Check Graphics API
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (apis.Length != 1 || apis[0] != GraphicsDeviceType.Vulkan)
            {
                Debug.LogError("[Audit FAIL] Android Graphics API must be exclusively Vulkan.");
                errors++;
            }

            // 2. Check Color Space
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                Debug.LogError("[Audit FAIL] Color Space must be Linear.");
                errors++;
            }

            // 3. Check URP Asset
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                Debug.LogError("[Audit FAIL] No UniversalRenderPipelineAsset assigned in GraphicsSettings.");
                errors++;
            }
            else
            {
                if (urpAsset.supportsHDR) { Debug.LogError("[Audit FAIL] URP HDR must be Disabled."); errors++; }
                if (urpAsset.msaaSampleCount != 2) { Debug.LogError("[Audit FAIL] URP MSAA must be 2x."); errors++; }
                if (urpAsset.supportsCameraDepthTexture) { Debug.LogError("[Audit FAIL] Camera Depth Texture must be Disabled."); errors++; }
                if (urpAsset.supportsCameraOpaqueTexture) { Debug.LogError("[Audit FAIL] Camera Opaque Texture must be Disabled."); errors++; }
                if (!urpAsset.useSRPBatcher) { Debug.LogError("[Audit FAIL] SRP Batcher must be Enabled."); errors++; }
                if (urpAsset.shadowCascadeCount != 1) { Debug.LogError("[Audit FAIL] Shadow Cascades must be set to 1."); errors++; }
            }

            // 4. Check Render Graph Compatibility Mode (Unity 6)
            var renderGraphSettings = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
            if (renderGraphSettings != null && renderGraphSettings.enableRenderCompatibilityMode)
            {
                Debug.LogError("[Audit FAIL] Render Graph Compatibility Mode is enabled! Must be disabled for native Render Graph.");
                errors++;
            }

            if (errors == 0)
            {
                Debug.Log("<color=#00FF88>[Audit PASS] All Phase 1 Engine, Vulkan, and URP gates passed!</color>");
            }
            else
            {
                Debug.LogWarning($"[Audit Complete] Found {errors} compliance issue(s). Run 'Enforce Player & URP Settings'.");
            }
        }

        private static void EnsureSettingsDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }
        }

        private static void ConfigureAndroidPlayerSettings()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.MTRendering = true;
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
            PlayerSettings.gpuSkinning = true;
        }

        private static void ConfigureUrpPipelineAssets()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            }

            if (urpAsset == null)
            {
                Debug.LogWarning("[Phase 1 Configurator] Please assign your UniversalRenderPipelineAsset in Project Settings > Graphics first, then re-run.");
                return;
            }

            // Core URP Settings per Phase 1 Spec
            urpAsset.supportsHDR = false;
            urpAsset.msaaSampleCount = 2;
            urpAsset.renderScale = 1.0f;
            urpAsset.supportsCameraDepthTexture = false;
            urpAsset.supportsCameraOpaqueTexture = false;
            urpAsset.useSRPBatcher = true;
            urpAsset.supportsDynamicBatching = false;

            // Lighting & Shadow Discipline
            urpAsset.mainLightRenderingMode = LightRenderingMode.PerPixel;
            urpAsset.supportsMainLightShadows = true;
            urpAsset.mainLightShadowmapResolution = 1024;
            urpAsset.shadowDistance = 15.0f;
            urpAsset.shadowCascadeCount = 1;
            urpAsset.additionalLightsRenderingMode = LightRenderingMode.Disabled;
            urpAsset.reflectionProbeBlending = false;
            urpAsset.reflectionProbeBoxProjection = false;

            // Explicitly disable GPU Resident Drawer via SerializedObject (Unity 6 property)
            SerializedObject serializedUrp = new SerializedObject(urpAsset);
            SerializedProperty gpuResidentDrawerProp = serializedUrp.FindProperty("m_GPUResidentDrawerMode");
            if (gpuResidentDrawerProp != null)
            {
                gpuResidentDrawerProp.intValue = 0; // 0 = Disabled
            }
            serializedUrp.ApplyModifiedProperties();

            // Inspect Renderer Data to disable Depth Priming & enforce Forward mode
            ScriptableRendererData[] rendererDataList = GetRendererDataList(urpAsset);
            if (rendererDataList != null)
            {
                foreach (var data in rendererDataList)
                {
                    if (data is UniversalRendererData universalRenderer)
                    {
                        universalRenderer.renderingMode = RenderingMode.Forward;
                        universalRenderer.depthPrimingMode = DepthPrimingMode.Disabled;
                        universalRenderer.copyDepthMode = CopyDepthMode.AfterTransparents; // Prevents mid-pass depth resolves
                        EditorUtility.SetDirty(universalRenderer);
                    }
                }
            }

            // Enforce Native Render Graph (Disable Compatibility Mode)
            var renderGraphSettings = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
            if (renderGraphSettings != null)
            {
                renderGraphSettings.enableRenderCompatibilityMode = false;
            }

            EditorUtility.SetDirty(urpAsset);
        }

        private static ScriptableRendererData[] GetRendererDataList(UniversalRenderPipelineAsset asset)
        {
            FieldInfo field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(asset) as ScriptableRendererData[];
        }
    }
}
#endif
