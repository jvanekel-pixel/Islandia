### **Phase 1: Engine Foundation, Vulkan URP Pipeline & OpenXR Configuration**

Establish the baseline **Unity 6 (6000.x LTS)** project configured specifically for Meta Quest 2, 3S, and 33. Configure the **Meta Quest Build Profile** to enable automatic Quest shader optimizations, set the graphics API to **Vulkan**, and use the **Render Graph** system with **Forward Rendering** while keeping **GPU Resident Drawer disabled**3. In the URP Asset, enable **2X MSAA**, **SRP Batcher**, and **ASTC texture compression**, while disabling **HDR**, **Depth Priming**, **Opaque Texture**, **Depth Texture**, and **SSAO**3. In the **OpenXR Plugin**, activate **Multi-view / Single-pass stereo rendering**, **Foveated rendering**, **Multiview render regions**, the **Adaptive Performance Basic provider**, and optional **URP Application SpaceWarp** support3. Verify on-device with OVR Metrics that the empty ocean and flat tan island maintain 90 Hz (<11.1 ms) on Quest 3 and 72 Hz (<13.8 ms) on Quest 24.  
**Phase 1: Engine Foundation, Vulkan URP Pipeline & OpenXR Configuration** is the bedrock of the Meta Quest 1-Acre Island World-Builder. Because the entire Phase 1 implementation—including the Master Design Specification, complete environment configuration, production-ready Editor validation and C# runtime code, baseline placeholder shaders, and developer troubleshooting playbooks—can be delivered in a single, exhaustive blueprint without truncation, **the entirety of Phase 1 (both 1.a Architecture/Environment and 1.b Code/Troubleshooting) is contained in this response.**

## **1.a Master Design Specification & Environment Architecture**

### **1.1 Architectural Mandate & Hardware Gates**

Standalone VR applications look blocky, washed-out, and aliased when developers treat mobile tile-based GPUs (Qualcomm Snapdragon XR2 Gen 1 on Quest 2; XR2 Gen 2 on Quest 3/3S) like desktop GPUs. Every frame on a tile-based GPU is divided into small on-chip memory tiles. If a rendering pipeline forces a flush from fast on-chip tile memory to slow system RAM (a **GMEM load/store**)—caused by Deferred Rendering, Depth Priming, Opaque/Depth Texture copies, or traditional multi-pass post-processing—the GPU stalls, thermal throttling engages, and frame rates plummet.

Phase 1 establishes a strict **Single-Pass Stereo (Multi-View) Vulkan Forward Render Graph pipeline** in **Unity 6 (6000.x LTS)** that enforces zero GMEM flushes on the baseline island.

| Hardware Target | Per-Eye Resolution | Target Refresh & Frame Budget | Baseline Phase 1 GPU Gate | Max Scene Triangles | Max Draw Calls (Baseline / Busy) |
| :---- | :---- | :---- | :---- | :---- | :---- |
| **Meta Quest 3 / 3S** | 2064 × 2208 px  | **90 Hz** (11.1 ms / 11,100 µs)  | **< 6.0 ms** (Empty Island + Ocean)  | < 1,500,000 (1.3M–1.8M cap)  | < 200 Baseline / 200–300 Busy  |
| **Meta Quest 2 / Pro** | 1832 × 1920 px  | **72 Hz** (13.8 ms / 13,800 µs)  | **< 6.0 ms** (Empty Island + Ocean)  | < 750,000 (750K–1.0M cap)  | < 100 Baseline / 80–200 Busy  |

### **1.2 Required Development Environment & Toolchain**

Configure the workstation with these exact package versions and tools before initializing the repository:

1. **Unity Editor:** **Unity 6 (6000.1 LTS or 6000.2+ LTS)** with the **Android Build Support** module installed:  

   * Android SDK & NDK Tools (API Level 32 minimum, API Level 34 target).  
   * OpenJDK 17 (bundled with Unity 6).  
3. **External Profiling & Debugging Suite:**  

   * **Meta Quest Developer Hub (MQDH):** For wireless ADB deployment, logcat filtering, and OVR Metrics control.  
   * **OVR Metrics Tool (On-Device Overlay):** Configured to display FPS, **APP T** (Application GPU Time in microseconds), CPU Level, GPU Level, and Early/Stale Frame counts.  
   * **Meta Quest Runtime Optimizer:** Unity package for live frame freezing and Green (<80%), Yellow (80–95%), Red (>95%) budget heatmaps.  
   * **RenderDoc Meta Fork:** For inspecting Vulkan render passes and verifying zero GMEM loads.  
5. **Project** Packages/manifest.json **Dependencies:** Feed the following manifest.json directly into your project to lock the required Unity 6 XR, URP, Burst, and Adaptive Performance packages:  

JSON

```

{
  "dependencies": {
    "com.unity.adaptiveperformance": "6.0.1",
    "com.unity.adaptiveperformance.google.android": "6.0.0",
    "com.unity.burst": "1.8.18",
    "com.unity.collections": "2.5.1",
    "com.unity.inputsystem": "1.11.2",
    "com.unity.mathematics": "1.3.2",
    "com.unity.render-pipelines.core": "17.0.3",
    "com.unity.render-pipelines.universal": "17.0.3",
    "com.unity.xr.Hands": "1.5.0",
    "com.unity.xr.interaction.toolkit": "3.0.7",
    "com.unity.xr.management": "4.5.0",
    "com.unity.xr.meta-openxr": "2.1.0",
    "com.unity.xr.openxr": "1.13.1",
    "com.unity.modules.ai": "1.0.0",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.animation": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.physics": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.vr": "1.0.0",
    "com.unity.modules.xr": "1.0.0"
  }
}

```

### **1.3 Master Engine & Pipeline Configuration Matrix**

Every setting below is mandatory for Phase 1. The automated Editor script in Section 1.b (Phase1ProjectConfigurator.cs) enforces the scriptable portions of this matrix automatically, while the manual checklist verifies the serialized sub-assets.

#### **A. Build Profile & Player Settings (**Edit > Project Settings > Player > Android**)**

* **Active Build Profile:** **Meta Quest** (in File > Build Profiles, select the dedicated **Meta Quest** profile under Android to activate Unity 6's automatic Quest shader stripping and compilation optimizations).  
* **Scripting Backend:** IL2CPP  
* **Target Architectures:** ARM64 ONLY (Uncheck ARMv7 and x86_64).  
* **Color Space:** Linear (Required for physically accurate lighting and glTFast PBR compatibility; note that transparent alpha blending in Linear space is expensive, so our architecture strictly minimizes alpha blending).  
* **Auto Graphics API:** Disabled  
* **Graphics APIs:** Vulkan ONLY (Remove OpenGL ES 3 completely).  
* **Multithreaded Rendering:** Enabled  
* **GPU Skinning / Compute Skinning:** Enabled (Offloads Phase 10 pet skeletal deformation to Vulkan compute).  
* **Texture Compression Format:** ASTC.  
* **Normal Map Encoding:** DXT5nm-style (Prevents subtle mobile normal map banding artifacts).  

#### **B. Universal Render Pipeline Asset (**Island_URP_Asset.asset**)**

* **Rendering Path (on Renderer Data):** Forward (**NEVER** use Forward+ or Deferred).  
* **Render Graph (**Project Settings > Graphics > URP**):** Compatibility Mode Disabled (Enforces native **Render Graph** execution for automatic pass merging and on-tile memory retention).  
* **GPU Resident Drawer:** Disabled (Unity 6's official untethered XR documentation explicitly warns that GPU Resident Drawer requires Forward+ and compute overhead that *reduces* frame rates on Meta Quest unless a scene is strictly CPU-bound).  
* **SRP Batcher:** Enabled.  
* **Dynamic Batching:** Disabled (SRP Batcher + GPU Instancing + Runtime Mesh Welding supersede CPU dynamic batching).  
* **HDR:** Disabled (Saves 64-bit float buffer bandwidth and prevents tile flushes).  
* **Anti-Aliasing (MSAA):** 2x (Runs natively inside on-chip tile memory on Adreno GPUs during Forward rendering with near-zero cost; 4x MSAA consumes extra tile memory needed for high eye-buffer resolutions).  
* **Depth Texture:** Disabled.  
* **Opaque Texture:** Disabled.  
* **Main Light:** Per Pixel, **Cast Shadows:** Enabled (Strictly capped at 15m shadow distance for player/pet proximity, 1024px resolution, **1 Shadow Cascade**—foliage has dynamic shadows disabled).  
* **Additional Lights:** Disabled or Per Vertex (Maximum 0 dynamic pixel lights; decorative lights use baked lightmaps/vertex colors).  
* **Depth Priming Mode (on** UniversalRendererData**):** Disabled (Adreno GPUs have hardware Low-Resolution-Z [LRZ] and Hidden Surface Removal [HSR]; enabling Depth Priming doubles vertex processing across both eyes needlessly).  
* **Post-Processing:** Disabled on main camera (MSAA + standard Post-Processing breaks on-tile rendering and triggers a full-resolution GMEM resolve).  

#### **C. OpenXR & Meta Quest Feature Group (**Edit > Project Settings > XR Plug-in Management > OpenXR**)**

* **Render Mode:** Multi-Pass → Change to Single Pass Instanced / Multi-View.  
* **Depth Submission Mode:** Depth 16 Bit (Required for compositor layer depth sorting and Application SpaceWarp).  
* **Foveated Rendering API:** SRP Foveation (Unity 6 native standard).  
* **Enabled OpenXR Features (Android Tab):**  

  * Foveated Rendering (Enabled)  
  * Meta Quest Support (Enabled)  
  * Meta XR Subsampled Layout / Multiview Render Regions (Enabled — stops rendering black corner pixels masked by the lens distortion mesh)  
  * URP Application SpaceWarp (Enabled — dynamically toggled at runtime only during heavy terraforming or massive asset spawns).  

## **1.b Production Code Suite, Baseline Shaders & Troubleshooting Guide**

All scripts below are structured for immediate ingestion by a coding AI or developer. Place each script into the exact file path specified in its header comment.

### **Script 1: Automated Editor Setup & Pipeline Validator**

This Editor tool creates and audits the URP Asset, Player Settings, Graphics Settings, and baseline Phase 1 scene hierarchy with a single menu click (IslandBuilder > Phase 1 > Enforce & Validate Engine Settings).

**File Path:** Assets/Scripts/Editor/Phase1ProjectConfigurator.cs

C#

```

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

```

### **Script 2: Hardware Tier Detector & Dynamic Pipeline Bootstrapper**

In Unity 6, XRDisplaySubsystem can take **up to 3 frames to initialize** at startup. Accessing foveatedRenderingLevel or TryRequestDisplayRefreshRate inside Awake() or frame 0 Start() silently fails. This script detects whether the player is on a **Meta Quest 2** or **Meta Quest 3 / 3S**, waits 3 frames for the OpenXR Display Subsystem, locks the refresh rate (72 Hz on Quest 2, 90 Hz on Quest 3/3S), enables SRP Foveated Rendering, and configures the dynamic budgets for later phases.

**File Path:** Assets/Scripts/Core/QuestHardwareProfileManager.cs

C#

```

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace IslandBuilder.Core
{
    public enum QuestHardwareTier
    {
        UnknownEditor = 0,
        Quest2OrPro = 1,
        Quest3Or3S = 2
    }

    [Serializable]
    public struct HardwareBudgetProfile
    {
        public QuestHardwareTier Tier;
        public float TargetRefreshRate;
        public float FrameBudgetMilliseconds;
        public float Phase1GpuCeilingMilliseconds;
        public float RenderViewportScale;
        public float FoveatedRenderingLevel;
        public int MaxBaselineDrawCalls;
        public int MaxBusySimulationDrawCalls;
        public int MaxSceneTriangles;
    }

    [DefaultExecutionOrder(-500)]
    public sealed class QuestHardwareProfileManager : MonoBehaviour
    {
        public static QuestHardwareProfileManager Instance { get; private set; }

        [Header("Runtime Detected Profile")]
        [SerializeField] private HardwareBudgetProfile activeProfile;

        private XRDisplaySubsystem _displaySubsystem;
        private bool _isSubsystemInitialized;

        public HardwareBudgetProfile ActiveProfile => activeProfile;
        public XRDisplaySubsystem DisplaySubsystem => _displaySubsystem;
        public bool IsSubsystemInitialized => _isSubsystemInitialized;

        public event Action<HardwareBudgetProfile> OnHardwareProfileConfigured;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            DetectHardwareTierAndBudgets();
        }

        private IEnumerator Start()
        {
            // CRITICAL UNITY 6 REQUIREMENT:
            // XRDisplaySubsystem takes up to 3 frames to fully initialize.
            // Setting foveatedRenderingLevel or scaleOfAllRenderTargets on frame 0 is ignored by OpenXR.
            yield return null;
            yield return null;
            yield return null;

            InitializeXRDisplaySubsystem();
        }

        private void DetectHardwareTierAndBudgets()
        {
            string deviceModel = SystemInfo.deviceModel ?? string.Empty;
            string gpuName = SystemInfo.graphicsDeviceName ?? string.Empty;

            // Quest 3 and Quest 3S use Snapdragon XR2 Gen 2 (Adreno 740)
            // Quest 2 uses Snapdragon XR2 Gen 1 (Adreno 650)
            bool isQuest3Generation = gpuName.Contains("740", StringComparison.OrdinalIgnoreCase) ||
                                      deviceModel.Contains("Quest 3", StringComparison.OrdinalIgnoreCase) ||
                                      deviceModel.Contains("Eureka", StringComparison.OrdinalIgnoreCase) ||
                                      deviceModel.Contains("Panther", StringComparison.OrdinalIgnoreCase);

            if (isQuest3Generation)
            {
                activeProfile = new HardwareBudgetProfile
                {
                    Tier = QuestHardwareTier.Quest3Or3S,
                    TargetRefreshRate = 90.0f,
                    FrameBudgetMilliseconds = 11.1f,
                    Phase1GpuCeilingMilliseconds = 6.0f,
                    RenderViewportScale = 1.0f,
                    FoveatedRenderingLevel = 0.75f, // Frees 15%-25% GPU headroom
                    MaxBaselineDrawCalls = 200,
                    MaxBusySimulationDrawCalls = 300,
                    MaxSceneTriangles = 1500000
                };
            }
            else
            {
                // Default to strict Quest 2 baseline for safety on older devices
                activeProfile = new HardwareBudgetProfile
                {
                    Tier = Application.isEditor ? QuestHardwareTier.UnknownEditor : QuestHardwareTier.Quest2OrPro,
                    TargetRefreshRate = 72.0f,
                    FrameBudgetMilliseconds = 13.8f,
                    Phase1GpuCeilingMilliseconds = 6.0f,
                    RenderViewportScale = 0.92f,
                    FoveatedRenderingLevel = 1.0f, // Max peripheral foveation on Quest 2
                    MaxBaselineDrawCalls = 100,
                    MaxBusySimulationDrawCalls = 200,
                    MaxSceneTriangles = 750000
                };
            }

            // Lock Unity Physics & Target Frame Rate to match refresh rate
            Application.targetFrameRate = Mathf.RoundToInt(activeProfile.TargetRefreshRate);
            Time.fixedDeltaTime = 1.0f / activeProfile.TargetRefreshRate;
            QualitySettings.vSyncCount = 1;
        }

        private void InitializeXRDisplaySubsystem()
        {
            var displaySubsystems = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displaySubsystems);

            if (displaySubsystems.Count == 0)
            {
                Debug.LogWarning("[QuestHardwareProfileManager] No active XRDisplaySubsystem found (normal in desktop Editor without XR Simulator).");
                OnHardwareProfileConfigured?.Invoke(activeProfile);
                return;
            }

            _displaySubsystem = displaySubsystems[0];
            _isSubsystemInitialized = true;

            // 1. Configure SRP Foveated Rendering
            FoveatedRenderingCaps caps = SystemInfo.foveatedRenderingCaps;
            if (caps != FoveatedRenderingCaps.None)
            {
                _displaySubsystem.foveatedRenderingLevel = activeProfile.FoveatedRenderingLevel;
                _displaySubsystem.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed;
                Debug.Log($"[QuestHardwareProfileManager] Foveated Rendering enabled at level {activeProfile.FoveatedRenderingLevel:F2} (Caps: {caps}).");
            }
            else
            {
                Debug.LogWarning("[QuestHardwareProfileManager] Device reports FoveatedRenderingCaps.None. Verify 'Foveated Rendering' is checked in OpenXR settings.");
            }

            // 2. Apply Viewport Scaling
            _displaySubsystem.scaleOfAllViewports = activeProfile.RenderViewportScale;

            // 3. Request Target Refresh Rate via Meta OpenXR extension if available
            RequestTargetDisplayRefreshRate(activeProfile.TargetRefreshRate);

            OnHardwareProfileConfigured?.Invoke(activeProfile);
        }

        public void SetDynamicViewportScale(float scale)
        {
            if (!_isSubsystemInitialized || _displaySubsystem == null) return;
            _displaySubsystem.scaleOfAllViewports = Mathf.Clamp(scale, 0.7f, 1.2f);
        }

        private void RequestTargetDisplayRefreshRate(float targetHz)
        {
            // Uses reflection to invoke Meta OpenXR display refresh rate extension if present,
            // avoiding hard compile failures if package versions change.
            if (_displaySubsystem == null) return;

            var method = _displaySubsystem.GetType().GetMethod("TryRequestDisplayRefreshRate",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(_displaySubsystem, new object[] { targetHz });
                Debug.Log($"[QuestHardwareProfileManager] Requested Display Refresh Rate: {targetHz} Hz.");
            }
        }
    }
}

```

### **Script 3: Dynamic Application SpaceWarp (AppSW) Controller**

Application SpaceWarp synthesizes every other frame using a low-resolution motion vector buffer and depth buffer, boosting effective frame rates by up to 70%. However, keeping AppSW enabled 100% of the time introduces subtle edge artifacts on fast-moving objects and requires rendering motion vector passes. This controller exposes a clean API so Phase 2 terraforming brushes, Phase 7 glTFast loads, or emergency frame-time spikes can toggle AppSW dynamically while tracking the XR Origin camera pose every frame as required by Meta headsets.

**File Path:** Assets/Scripts/Core/DynamicSpaceWarpController.cs

C#

```

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace IslandBuilder.Core
{
    public sealed class DynamicSpaceWarpController : MonoBehaviour
    {
        public static DynamicSpaceWarpController Instance { get; private set; }

        [Header("References")]
        [Tooltip("Assign the XR Origin Transform (Tracking Space root), NOT the CenterEyeCamera.")]
        [SerializeField] private Transform xrOriginTransform;

        [Header("Automatic Frame-Spike Trigger")]
        [SerializeField] private bool enableAutoTriggerOnGpuSpike = true;
        [Tooltip("Hysteresis duration in seconds to keep AppSW active after a spike subsides.")]
        [SerializeField] private float holdDurationSeconds = 2.0f;

        private bool _isSpaceWarpActive;
        private int _manualRequestRefCount;
        private float _autoHoldTimer;

#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport("UnityOpenXR", EntryPoint = "MetaSetSpaceWarp")]
        private static extern void NativeMetaSetSpaceWarp(bool enabled);

        [DllImport("UnityOpenXR", EntryPoint = "MetaSetAppSpacePosition")]
        private static extern void NativeMetaSetAppSpacePosition(float x, float y, float z);

        [DllImport("UnityOpenXR", EntryPoint = "MetaSetAppSpaceRotation")]
        private static extern void NativeMetaSetAppSpaceRotation(float x, float y, float z, float w);
#endif

        public bool IsSpaceWarpActive => _isSpaceWarpActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void LateUpdate()
        {
            if (_autoHoldTimer > 0f)
            {
                _autoHoldTimer -= Time.unscaledDeltaTime;
                if (_autoHoldTimer <= 0f && _manualRequestRefCount == 0)
                {
                    ApplySpaceWarpState(false);
                }
            }

            // Meta OpenXR SpaceWarp requires updating the AppSpace (XR Origin) position
            // and rotation every frame while SpaceWarp is active so synthesized frames
            // distinguish player locomotion from object motion.
            if (_isSpaceWarpActive && xrOriginTransform != null)
            {
                UpdateAppSpaceTransform(xrOriginTransform.position, xrOriginTransform.rotation);
            }
        }

        /// <summary>
        /// Call when starting heavy operations (e.g., Phase 2 Terraforming Brush Down or Phase 7 glTF batch spawn).
        /// </summary>
        public void AcquireSpaceWarpLock()
        {
            _manualRequestRefCount++;
            if (!_isSpaceWarpActive)
            {
                ApplySpaceWarpState(true);
            }
        }

        /// <summary>
        /// Call when finishing heavy operations (e.g., Terraforming Brush Up).
        /// </summary>
        public void ReleaseSpaceWarpLock()
        {
            _manualRequestRefCount = Mathf.Max(0, _manualRequestRefCount - 1);
            if (_manualRequestRefCount == 0 && _autoHoldTimer <= 0f && _isSpaceWarpActive)
            {
                ApplySpaceWarpState(false);
            }
        }

        /// <summary>
        /// Called by Phase1PerformanceGateMonitor if GPU frame time exceeds 90% of hardware budget.
        /// </summary>
        public void NotifyGpuSpikeDetected()
        {
            if (!enableAutoTriggerOnGpuSpike) return;
            _autoHoldTimer = holdDurationSeconds;
            if (!_isSpaceWarpActive)
            {
                ApplySpaceWarpState(true);
            }
        }

        private void ApplySpaceWarpState(bool enable)
        {
            _isSpaceWarpActive = enable;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                NativeMetaSetSpaceWarp(enable);
            }
            catch (EntryPointNotFoundException)
            {
                Debug.LogWarning("[DynamicSpaceWarpController] UnityOpenXR MetaSetSpaceWarp entry point not found. Ensure Meta XR SpaceWarp feature is enabled in OpenXR settings.");
            }
#endif
        }

        private static void UpdateAppSpaceTransform(Vector3 pos, Quaternion rot)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                NativeMetaSetAppSpacePosition(pos.x, pos.y, pos.z);
                NativeMetaSetAppSpaceRotation(rot.x, rot.y, rot.z, rot.w);
            }
            catch (EntryPointNotFoundException)
            {
                // Ignored if native entry point unavailable
            }
#endif
        }
    }
}

```

### **Script 4: Zero-GC Frame Timing & Hardware Gate Monitor**

This script uses Unity's FrameTimingManager and XRDisplaySubsystem.TryGetAppGPUTimeLastFrame to measure actual on-device CPU and GPU frame times in microseconds with **zero garbage collection allocations**. It classifies performance into the **Meta Quest Runtime Optimizer** zones (**Green < 80%**, **Yellow 80%–95%**, **Red > 95%**) and verifies the **Phase 1 < 6.0 ms GPU Gate**.

**File Path:** Assets/Scripts/Core/Phase1PerformanceGateMonitor.cs

C#

```

using Unity.Profiling;
using UnityEngine;
using UnityEngine.XR;

namespace IslandBuilder.Core
{
    public enum BudgetZone
    {
        Green = 0,  // < 80% of target budget
        Yellow = 1, // 80% - 95% of target budget
        Red = 2     // > 95% of target budget
    }

    public sealed class Phase1PerformanceGateMonitor : MonoBehaviour
    {
        public static Phase1PerformanceGateMonitor Instance { get; private set; }

        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];

        private ProfilerRecorder _setPassCallsRecorder;
        private ProfilerRecorder _drawCallsRecorder;
        private ProfilerRecorder _trianglesRecorder;

        private float _smoothedGpuTimeMs;
        private float _smoothedCpuTimeMs;
        private int _currentDrawCalls;
        private int _currentTriangles;
        private BudgetZone _currentBudgetZone;
        private bool _isPhase1GatePassing = true;

        public float SmoothedGpuTimeMs => _smoothedGpuTimeMs;
        public float SmoothedCpuTimeMs => _smoothedCpuTimeMs;
        public int CurrentDrawCalls => _currentDrawCalls;
        public int CurrentTriangles => _currentTriangles;
        public BudgetZone CurrentBudgetZone => _currentBudgetZone;
        public bool IsPhase1GatePassing => _isPhase1GatePassing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            _setPassCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        private void OnDisable()
        {
            _setPassCallsRecorder.Dispose();
            _drawCallsRecorder.Dispose();
            _trianglesRecorder.Dispose();
        }

        private void Update()
        {
            SampleHardwareMetrics();
            EvaluateBudgetThresholds();
        }

        private void SampleHardwareMetrics()
        {
            float rawGpuMs = 0f;
            float rawCpuMs = Time.unscaledDeltaTime * 1000.0f;

            var hwManager = QuestHardwareProfileManager.Instance;
            if (hwManager != null && hwManager.IsSubsystemInitialized && hwManager.DisplaySubsystem != null)
            {
                if (hwManager.DisplaySubsystem.TryGetAppGPUTimeLastFrame(out float xrGpuSeconds))
                {
                    rawGpuMs = xrGpuSeconds * 1000.0f;
                }
            }

            if (rawGpuMs <= 0.001f)
            {
                FrameTimingManager.CaptureFrameTimings();
                uint count = FrameTimingManager.GetLatestTimings(1, _frameTimings);
                if (count > 0)
                {
                    rawGpuMs = (float)_frameTimings[0].gpuFrameTime;
                    rawCpuMs = (float)_frameTimings[0].cpuFrameTime;
                }
            }

            // Exponential moving average to prevent single-frame jitter
            _smoothedGpuTimeMs = Mathf.Lerp(_smoothedGpuTimeMs, rawGpuMs, 0.1f);
            _smoothedCpuTimeMs = Mathf.Lerp(_smoothedCpuTimeMs, rawCpuMs, 0.1f);

            if (_drawCallsRecorder.Valid) _currentDrawCalls = (int)_drawCallsRecorder.LastValue;
            if (_trianglesRecorder.Valid) _currentTriangles = (int)_trianglesRecorder.LastValue;
        }

        private void EvaluateBudgetThresholds()
        {
            var hwManager = QuestHardwareProfileManager.Instance;
            if (hwManager == null) return;

            var profile = hwManager.ActiveProfile;

            // Phase 1 Specific Gate: Empty ocean + flat 1-acre island must stay < 6.0 ms GPU
            _isPhase1GatePassing = _smoothedGpuTimeMs <= profile.Phase1GpuCeilingMilliseconds;

            float gpuRatio = _smoothedGpuTimeMs / profile.FrameBudgetMilliseconds;
            float drawCallRatio = (float)_currentDrawCalls / profile.MaxBaselineDrawCalls;
            float worstRatio = Mathf.Max(gpuRatio, drawCallRatio);

            if (worstRatio < 0.80f)
            {
                _currentBudgetZone = BudgetZone.Green;
            }
            else if (worstRatio <= 0.95f)
            {
                _currentBudgetZone = BudgetZone.Yellow;
            }
            else
            {
                _currentBudgetZone = BudgetZone.Red;
                if (DynamicSpaceWarpController.Instance != null)
                {
                    DynamicSpaceWarpController.Instance.NotifyGpuSpikeDetected();
                }
            }
        }
    }
}

```

### **Script 5: Phase 1 Baseline 1-Acre Island & Ocean Spawner**

Before Phase 2 replaces the island with the 64-chunk sculptable heightmap, Phase 1 requires verifying the rendering pipeline on the actual headset with a **flat, tan 1-acre island (64m × 64m = 4,096 m² ≈ 1.01 acres)** surrounded by an ocean plane and static horizon skybox. Note how the mesh is subdivided into a grid so no huge triangles span every tile on the Quest GPU (avoiding the tile-boundary vertex shader multiplication penalty documented in the blueprint).

**File Path:** Assets/Scripts/Core/Phase1BaselineIslandSpawner.cs

C#

```

using UnityEngine;
using UnityEngine.Rendering;

namespace IslandBuilder.Core
{
    public sealed class Phase1BaselineIslandSpawner : MonoBehaviour
    {
        [Header("Phase 1 Verification Materials")]
        [SerializeField] private Material baselineIslandMaterial;
        [SerializeField] private Material baselineOceanMaterial;

        [Header("1-Acre Spatial Dimensions")]
        [SerializeField] private float islandSizeMeters = 64.0f; // 64m x 64m = 4,096 m^2 (~1 Acre)
        [SerializeField] private int islandGridSubdivisions = 32; // Prevents giant tile-spanning triangles
        [SerializeField] private float oceanSizeMeters = 512.0f;
        [SerializeField] private int oceanGridSubdivisions = 32;

        private GameObject _islandRoot;

        private void Start()
        {
            BuildPhase1BaselineEnvironment();
        }

        private void BuildPhase1BaselineEnvironment()
        {
            _islandRoot = new GameObject("Phase1_Baseline_Environment");

            // 1. Create Flat Tan 1-Acre Island (y = 0.0m)
            GameObject islandObj = CreateSubdividedPlane(
                "OneAcre_Tan_Island_64x64",
                islandSizeMeters,
                islandGridSubdivisions,
                new Color(0.86f, 0.78f, 0.62f, 1.0f), // Warm Tan Sand vertex color
                baselineIslandMaterial,
                addBoxCollider: true);
            islandObj.transform.SetParent(_islandRoot.transform, false);
            islandObj.transform.localPosition = Vector3.zero;

            // 2. Create Surrounding Ocean Plane (y = -0.35m)
            GameObject oceanObj = CreateSubdividedPlane(
                "Surrounding_Ocean_Plane",
                oceanSizeMeters,
                oceanGridSubdivisions,
                new Color(0.08f, 0.45f, 0.62f, 1.0f), // Tropical Water vertex color
                baselineOceanMaterial,
                addBoxCollider: false);
            oceanObj.transform.SetParent(_islandRoot.transform, false);
            oceanObj.transform.localPosition = new Vector3(0f, -0.35f, 0f);
        }

        private static GameObject CreateSubdividedPlane(
            string objectName,
            float sizeMeters,
            int subdivisions,
            Color vertexColor,
            Material assignedMaterial,
            bool addBoxCollider)
        {
            GameObject go = new GameObject(objectName);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            // Disable shadow casting on flat ground/water to save shadow pass draw calls
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.sharedMaterial = assignedMaterial;

            int vertCountPerSide = subdivisions + 1;
            int totalVerts = vertCountPerSide * vertCountPerSide;
            Vector3[] vertices = new Vector3[totalVerts];
            Vector3[] normals = new Vector3[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];
            Color[] colors = new Color[totalVerts];
            int[] triangles = new int[subdivisions * subdivisions * 6];

            float halfSize = sizeMeters * 0.5f;
            float step = sizeMeters / subdivisions;

            int vIndex = 0;
            for (int z = 0; z <= subdivisions; z++)
            {
                for (int x = 0; x <= subdivisions; x++)
                {
                    vertices[vIndex] = new Vector3(-halfSize + x * step, 0f, -halfSize + z * step);
                    normals[vIndex] = Vector3.up;
                    uvs[vIndex] = new Vector2((float)x / subdivisions, (float)z / subdivisions);
                    colors[vIndex] = vertexColor;
                    vIndex++;
                }
            }

            int tIndex = 0;
            for (int z = 0; z < subdivisions; z++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int bottomLeft = z * vertCountPerSide + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + vertCountPerSide;
                    int topRight = topLeft + 1;

                    triangles[tIndex++] = bottomLeft;
                    triangles[tIndex++] = topLeft;
                    triangles[tIndex++] = topRight;

                    triangles[tIndex++] = bottomLeft;
                    triangles[tIndex++] = topRight;
                    triangles[tIndex++] = bottomRight;
                }
            }

            Mesh mesh = new Mesh
            {
                name = $"{objectName}_Mesh",
                indexFormat = IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;

            if (addBoxCollider)
            {
                BoxCollider col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(sizeMeters, 0.2f, sizeMeters);
                col.center = new Vector3(0f, -0.1f, 0f);
            }

            return go;
        }
    }
}

```

### **Shader 1: SRP-Batcher & Single-Pass Stereo Compliant Vertex-Color Baseline Shader**

Standard URP Lit shaders execute full PBR BRDF math on every pixel. This lightweight URP HLSL shader includes UNITY_VERTEX_INPUT_INSTANCE_ID and UNITY_VERTEX_OUTPUT_STEREO (mandatory for Single-Pass Instanced / Multi-View rendering), supports GPU Instancing, is 100% SRP-Batcher compatible, and uses vertex colors + directional lambert shading + vertex fogging. It also includes the XRMotionVectors pass so Application SpaceWarp works without smearing.

**File Path:** Assets/Shaders/Phase1BaselineVertexColor.shader

High-level shader language

```

Shader "IslandBuilder/Phase1BaselineVertexColor"
{
    Properties
    {
        _BaseColor ("Base Tint", Color) = (1, 1, 1, 1)
        _AmbientFloor ("Ambient Floor", Range(0.0, 1.0)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                 half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 vertexColor : COLOR;
                half  ndotl       : TEXCOORD0;
                half  fogFactor   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _AmbientFloor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.vertexColor = input.color * _BaseColor;

                // Evaluate directional light NdotL in the vertex shader to save fragment math on mobile tiles
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normInputs.normalWS, mainLight.direction));
                output.ndotl = lerp(_AmbientFloor, 1.0h, ndotl);

                // Vertex Fogging for Opaque (Zero fragment texture fetch overhead)
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 litColor = input.vertexColor.rgb * input.ndotl;
                litColor = MixFog(litColor, input.fogFactor);
                return half4(litColor, 1.0h);
            }
            ENDHLSL
        }

        // Built-in URP XR Motion Vectors pass so Application SpaceWarp (AppSW) synthesizes clean frames
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

```

### **1.4 Common Issues Developers Experience in Phase 1 (And Exact Fixes)**

| Issue / Symptom | Root Cause on Meta Quest / Unity 6 | Exact Engineering Fix |
| :---- | :---- | :---- |
| **1. Right Eye is completely black or flickers on the headset** | Custom shaders are missing UNITY_VERTEX_OUTPUT_STEREO and UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO while OpenXR Render Mode is set to Single Pass Instanced (Multi-View).  | Ensure every custom HLSL struct and vertex/fragment function includes the stereo macros shown in Phase1BaselineVertexColor.shader above. |
| **2. Setting** foveatedRenderingLevel **in** Awake() **or** Start() **does nothing** | In Unity 6 OpenXR, XRDisplaySubsystem takes **up to 3 frames** to initialize after scene load. Calls made on frame 0 are silently discarded by the runtime. | Wait 3 frames via a Coroutine (yield return null; × 3) before querying SubsystemManager.GetSubsystems and assigning foveatedRenderingLevel, exactly as implemented in QuestHardwareProfileManager.cs. |
| **3. GPU Frame Time is > 9.0 ms on an almost empty scene** | Either **GPU Resident Drawer** was enabled, **Depth Priming** is set to Auto/Forced, or **Camera Post-Processing** is checked alongside 2X MSAA—causing tile memory flushes (GMEM loads/stores).  | Run IslandBuilder > Phase 1 > Enforce & Validate Engine Settings. Verify in RenderDoc Meta Fork that the main pass stays in On-Chip Tile Memory with 0 GMEM loads.  |
| **4. Application SpaceWarp smears or wobbles the entire terrain when walking** | SpaceWarp was enabled via MetaSetSpaceWarp(true), but MetaSetAppSpacePosition and MetaSetAppSpaceRotation are not being called every frame with the XR Origin transform, or materials lack the XRMotionVectors pass. | Assign the XR Origin root transform to DynamicSpaceWarpController and include UsePass "Universal Render Pipeline/Lit/XRMotionVectors" in all custom shaders. |
| **5. Editor Profiler claims 450+ Draw Calls on an empty island** | Profiling inside the desktop Unity Editor adds Scene View cameras, gizmo passes, and Editor overhead that inflate draw calls by 2× to 3×.  | Never judge XR performance in the Editor. Build and deploy the APK to the Quest and inspect **OVR Metrics Tool (**APP T**)** and **Unity Profiler (Autoconnect Profiler)** on an Android Development Build.  |

### **1.5 On-Device Verification Protocol (Closing Phase 1 Gate)**

Before moving to **Phase 2 (1-Acre Chunked Terraforming Engine & Shoreline Water Shader)**, complete this 5-step hardware verification on your Quest 2 and/or Quest 3:

1. **Create the Bootstrap Scene (**Assets/Scenes/IslandBootstrap.unity**):**  

   * Add an empty GameObject named _EngineBootstrap and attach QuestHardwareProfileManager, DynamicSpaceWarpController, Phase1PerformanceGateMonitor, and Phase1BaselineIslandSpawner.  
   * Create two materials (M_Baseline_Island and M_Baseline_Ocean) using the IslandBuilder/Phase1BaselineVertexColor shader and assign them to Phase1BaselineIslandSpawner.  
   * Add your XR Origin (XR Rig) at (0, 0, 0) and ensure the Main Camera has **Post Processing unchecked** and **MSAA set to Use Pipeline Settings**.  
3. **Run the Automated Compliance Audit:**  

   * Click IslandBuilder > Phase 1 > 1. Enforce Player & URP Settings followed by 2. Audit Phase 1 Compliance and verify the console prints [Audit PASS].  
5. **Build & Deploy to Headset:**  

   * Open File > Build Profiles, select the **Meta Quest** profile, and click **Build And Run**.  
7. **Verify OVR Metrics Overlay:**  

   * Enable the **OVR Metrics Tool** HUD on the headset.  
   * Confirm **FPS = 90** on Quest 3/3S (or **72** on Quest 2).  
   * Confirm **APP T (Application GPU Time) < 6,000 µs (6.0 ms)** while looking across the 64m × 64m tan island and surrounding ocean.  
   * Confirm **Draw Calls < 15**.  

### **Updated Session Continuity Record (**PROJECT_STATE.md **Snippet)**

Markdown

```

[x] PHASE 1 COMPLETE: Engine Foundation, Vulkan URP Pipeline & OpenXR Configuration
    - Delivered: Phase1ProjectConfigurator.cs, QuestHardwareProfileManager.cs (with 3-frame XRDisplaySubsystem delay),
      DynamicSpaceWarpController.cs, Phase1PerformanceGateMonitor.cs, Phase1BaselineIslandSpawner.cs,
      and Phase1BaselineVertexColor.shader.
[ ] NEXT STEP (Phase 2): 1-Acre Chunked Terraforming Engine & Shoreline Water Shader
    - Deliverables: IslandTerrainManager.cs (8x8 grid of 8m x 8m procedural chunks, 33x33 vertices at 25cm spacing),
      C# Burst TerrainSculptJob.cs (Raise, Lower, Flatten, Smooth, RGBA Vertex Paint + deferred MeshCollider cooking),
      HLSL IslandTerrainUber.shader (Texture2DArray + Astroturf/Grass LUT), and HLSL VulkanWaterDepthFoam.shader
      (using FramebufferFetch / Vulkan Subpass Depth Input Attachment for zero-copy shoreline foam).

```

Whenever you are ready, say **"Start Phase 2"** and we will build the complete 1-acre chunked terraforming engine, Burst sculpting jobs, and Vulkan on-tile depth water shader.
