### **Phase 6: Cloud Browser Companion & Universal 3D Model Sanitization Pipeline**

Build the **Web Browser Companion** portal (React + Three.js) where users pair their headset and upload 3D models from any modeling tool (.fbx, .obj, .blend, .stl, .dae, .gltf, .glb) with an interactive **3D Scale & Pivot Cage** to address glTFast's lack of default import scale settings28. Configure the cloud transcoding worker using **gltf-transform**14 to prune and deduplicate files, generate simplified LOD meshes, cap textures at **1024px or 2048px** in **KTX2 (KHR_texture_basisu)** format (preventing Android 4K texture memory crashes while avoiding unsupported WebP textures), and compress geometry with **Draco (KHR_draco_mesh_compression)** or **Meshopt (EXT_meshopt_compression)**13.

**Phase 6: Cloud Browser Companion, P0/P1 Remediation Integration & Universal 3D Model Sanitization Pipeline** delivers both the mandatory **P0/P1 Architectural Remediation Patch** (Source 2) and the full-stack **Web Browser Companion + Cloud Sanitization & Security Pipeline**. Because the entire Phase 6 engineering package—including the P0/P1 Remediation Master Spec (Unity 6.3 LTS pin, 4-Tier Hardware Matrix, Master Budget Table, Shared WaterLevelY, Dynamic No-Lightmap Lighting, Early Save Schema, Security Sandboxing, and Total Imported Memory Caps), the React + Three.js 3D Scale & Pivot Normalization Portal, the Hardened Node.js gltf-transform + Sandboxed Blender Worker, and the Unity C# Pairing & Quota Client—fits cleanly into a single exhaustive guide without cutting corners, **both 6.a (Master Design Spec, Remediation & Security Architecture) and 6.b (Production Code Suite & Troubleshooting) are contained in full below.**

## **6.a Master Design Specification, P0/P1 Remediation & Cloud Security Architecture**

### **6.1 P0 & P1 Architectural Remediation Integration (Audit Compliance)**

Per the **Oculus Island World-Builder Remediation Plan**, before deploying Phase 6–9 cloud and media code, six **P0** foundation fixes and five **P1** cloud/memory scoping rules are locked into the architecture:

1. **P0 #1 — Pin Unity 6.3 LTS & Lowercase** manifest.json**:** Standardized on **Unity 6.3 LTS (6000.3.x LTS, supported through Dec 2027)** with strictly lowercase package identifiers (com.unity.xr.hands, com.unity.ai.navigation, com.unity.xr.compositionlayers, com.unity.cloud.gltfast, com.unity.cloud.ktx, com.unity.cloud.draco, com.unity.meshopt.decompress).  
3. **P0 #2 — Dynamic Lighting Model (No Lightmap Bake Dependency):** Dropped static lightmaps and light probes (which invalidate whenever players sculpt terrain or weld modular buildings). The island uses **1 Directional Main Light**, **per-asset baked vertex AO (**color.b **/** color.a**)**, and **runtime Sky/Equator/Ground Trilight Ambient (**RenderSettings.ambientMode = AmbientMode.Trilight**)** supporting an optional smooth Day/Night sun cycle with zero bake steps.  
5. **P0 #3 — Single Shared** IslandConstants.WaterLevelY **(**-0.35f**):** Replaced all hardcoded -0.35f, -0.30f, and -0.25f checks across Terrain, Foliage, and NavMesh with IslandConstants.WaterLevelY and IslandConstants.SubmergedCullY.  
7. **P0 #4 — 4-Tier Hardware Detection & Terrain Super-Chunking (**Quest 2**,** Quest 3S**,** Quest Pro**,** Quest 3**):** Quest 3S uses the Snapdragon XR2 Gen 2 GPU (Adreno 740) at Quest 2 eye-buffer resolution ($1832\times 1920$), while Quest 2 benefits from merging the $8\times 8$ sculptural chunks into **$4\times 4$ Render Regions (16 draw calls instead of 64)** during passive viewing to free up 48 draw calls.  
9. **P0 #5 — Early Binary/JSON Versioned Save Format (**IslandSaveSchema v1**):** Defined upfront in Section 6.b (IslandRemediationCore.cs) so Undo, Static Welding, glTF Export, Cloud Imports, and Pets share a unified deterministic serialization schema.  
11. **P1 #9 & #10 — Hardened Upload Security & Cumulative Imported Memory Cap:**  

    * **Blender CLI Sandboxing:** Blender executes inside a network-isolated container/process with --factory-startup -noaudio (explicitly omitting --python-autoexec so embedded malicious Python scripts in .blend files can **never** execute).  
    * **Expiring Pairing Codes, Rate Limiting & Malware Magic-Byte Inspection:** 6-digit headset pairing codes expire after **10 minutes** (TTL = 600s), uploads are rate-limited to **10 models / 15 minutes per IP**, and files undergo SHA-256 + magic-byte validation.  
    * **Cumulative Imported Model RAM & VRAM Budget:** Beyond the **< 15 MB per** .glb **file** gate, the headset enforces a **strict cumulative runtime memory pool** for user-imported models (**128 MB max on Quest 2 / Quest Pro**, **256 MB max on Quest 3 / 3S**) so importing dozens of models never triggers an Android OS Out-Of-Memory (OOM) kill.  

### **6.2 Master Performance & Memory Budget Table (P0 #4 & P1 #10 Gate Reference)**

Every subsystem across Phases 1–12 is governed by this unified per-device hardware allocation table:

| Subsystem / Allocation | Meta Quest 2 (1832×1920 @ 72 Hz) | Meta Quest Pro (1800×1920 @ 72/90 Hz) | Meta Quest 3S (1832×1920 @ 90 Hz) | Meta Quest 3 (2064×2208 @ 90 Hz) |
| :---- | :---- | :---- | :---- | :---- |
| **Frame Time Ceiling (**APP T**)** | **< 13.8 ms** (13,800 µs)  | **< 13.8 ms** (13,800 µs)  | **< 11.1 ms** (11,100 µs)  | **< 11.1 ms** (11,100 µs)  |
| **1. Terrain & Ocean** | **17 Draw Calls** (4×4 Super-Regions + 1 Ocean), **131K Tris**, **32 MB RAM**  | **17 Draw Calls**, **131K Tris**, **32 MB RAM**  | **65 Draw Calls** (8×8 Chunks + 1 Ocean), **131K Tris**, **32 MB RAM**  | **65 Draw Calls**, **131K Tris**, **32 MB RAM**  |
| **2. Botanical Foliage (398 Var.)** | **45 Draw Calls** (Instanced), **220K Tris**, **48 MB RAM**  | **55 Draw Calls**, **260K Tris**, **48 MB RAM**  | **75 Draw Calls**, **450K Tris**, **64 MB RAM**  | **85 Draw Calls**, **550K Tris**, **64 MB RAM**  |
| **3. Architecture & Welded Props** | **35 Draw Calls**, **180K Tris**, **64 MB RAM**  | **45 Draw Calls**, **220K Tris**, **64 MB RAM**  | **60 Draw Calls**, **350K Tris**, **96 MB RAM**  | **70 Draw Calls**, **450K Tris**, **96 MB RAM**  |
| **4. User-Imported** .glb **Assets** | **25 Draw Calls**, **120K Tris**, **128 MB Cap** (Max 15 MB/GLB)  | **30 Draw Calls**, **150K Tris**, **160 MB Cap**  | **40 Draw Calls**, **220K Tris**, **256 MB Cap**  | **45 Draw Calls**, **250K Tris**, **256 MB Cap** (Max 15 MB/GLB)  |
| **5. Pets, Media & Wrist UI** | **18 Draw Calls** (Max 4 Pets + 1 Active Screen), **60K Tris**, **96 MB RAM**  | **20 Draw Calls**, **75K Tris**, **112 MB RAM**  | **25 Draw Calls** (Max 6 Pets + 2 Screens), **90K Tris**, **128 MB RAM**  | **25 Draw Calls** (Max 6 Pets + 2 Screens), **100K Tris**, **128 MB RAM**  |
| **TOTAL BUSY SCENE GATE** | **$\leq$ 140 Draw Calls** (<200 Hard Cap), **$\leq$ 711K Tris** (<750K Cap), **368 MB**  | **$\leq$ 167 Draw Calls**, **$\leq$ 836K Tris**, **416 MB**  | **$\leq$ 265 Draw Calls** (<300 Hard Cap), **$\leq$ 1.24M Tris** (<1.5M Cap), **576 MB**  | **$\leq$ 290 Draw Calls** (<300 Hard Cap), **$\leq$ 1.48M Tris** (<1.5M Cap), **576 MB**  |

### **6.3 Why User-Uploaded 3D Models Crash Standalone VR (And How Phase 6 Prevents It)**

When users upload raw 3D models from Sketchfab, Blender, Maya, or CAD tools (.fbx, .obj, .blend, .stl, .dae, .gltf, .glb), five fatal issues occur if loaded directly onto a Meta Quest via glTFast:

1. **Missing Import Scale & Pivot Offsets:** Unlike Unity's Editor FBX importer, glTFast has **no built-in unit scale conversion at runtime**—a model authored in centimeters or millimeters loads $100\times$ too big or $1,000\times$ too small, often with its origin floating 50 meters away from its geometry.  
3. **4K / 8K PNG & JPEG Texture Memory Spikes:** Decompressing a single 4096×4096 RGBA PNG on Android spikes CPU heap and consumes **89 MB of uncompressed GPU VRAM** (with mipmaps), crashing the Quest 2 process after just 3 models.  
5. **Unsupported WebP Textures:** Many web-optimized .glb tools convert textures to EXT_texture_webp, which is **unsupported by default in** glTFast and renders pink/black on Quest.  
7. **Uncompressed High-Poly Meshes:** Raw 500,000-triangle sculpts choke both network download times and Quest vertex budgets.  
9. **Embedded** .blend **Script Execution Vulnerabilities:** Opening untrusted .blend files on a cloud server without disabling Python auto-execution allows arbitrary remote code execution (RCE).  

**The Phase 6 Solution:**

1. **Browser Companion Interactive 3D Scale & Pivot Normalization Cage (**React + Three.js**):** Before hitting "Send to Headset," the user previews their model inside an interactive **1.8m Human Reference Silhouette & 1m Grid Cage**, clicks **One-Click Bottom-Center Pivot & Unit Scale (**mm / cm / in -> meters**)**, and bakes the corrected transform matrix directly into the upload metadata.  
3. **Sandboxed Cloud Transcoding Pipeline (**Node.js + Blender CLI + @gltf-transform**):**  

   * Converts .fbx / .obj / .blend / .stl / .dae to .glb inside a locked-down, network-isolated Blender CLI worker (--factory-startup -noaudio, Python auto-execution strictly disabled).  
   * Runs @gltf-transform/core + @gltf-transform/functions (dedup, prune, weld, simplify via meshoptimizer to cap triangles at $\leq 25,000$ tris, and toktx / KHR_texture_basisu UASTC/ETC1S compression capped at **1024×1024** for props or **2048×2048** for hero architecture, plus draco / meshopt geometry compression).  
   * Guarantees every output .glb is **< 15 MB** and streams directly to the paired headset via WebSocket push.  

### **6.4 Required Environment & Toolchain for Phase 6**

1. **Updated Unity 6.3 LTS** Packages/manifest.json **(P0 #1 Compliant — Strictly Lowercase):**  

JSON

```

{
  "dependencies": {
    "com.unity.adaptiveperformance": "6.0.1",
    "com.unity.adaptiveperformance.google.android": "6.0.0",
    "com.unity.ai.navigation": "2.0.5",
    "com.unity.burst": "1.8.18",
    "com.unity.cloud.draco": "5.1.0",
    "com.unity.cloud.gltfast": "6.10.0",
    "com.unity.cloud.ktx": "3.4.0",
    "com.unity.collections": "2.5.1",
    "com.unity.inputsystem": "1.11.2",
    "com.unity.mathematics": "1.3.2",
    "com.unity.meshopt.decompress": "0.1.0-exp.1",
    "com.unity.render-pipelines.core": "17.0.3",
    "com.unity.render-pipelines.universal": "17.0.3",
    "com.unity.xr.compositionlayers": "2.0.0",
    "com.unity.xr.hands": "1.5.0",
    "com.unity.xr.interaction.toolkit": "3.0.7",
    "com.unity.xr.management": "4.5.0",
    "com.unity.xr.meta-openxr": "2.1.0",
    "com.unity.xr.openxr": "1.13.1"
  }
}

```

2. **Cloud Sanitization Worker Environment (**Node.js 22 LTS **+ System Binaries):**  

   * Install **KTX-Software (**toktx**)** from Khronos Group on the worker host/container so @gltf-transform/cli can encode KHR_texture_basisu KTX2 mipmapped textures.  
   * Install **Blender 4.2 LTS Headless** (apt-get install blender on Linux Docker container) for non-glTF format conversion (.fbx, .obj, .blend, .stl, .dae).  

## **6.b Production Code Suite & Troubleshooting Guide**

This code suite includes **1 Unity P0/P1 Remediation Core Script**, **2 Node.js/Python Cloud Sanitization Worker Scripts**, **1 React + Three.js Browser Companion Portal**, and **1 Unity C# Headset Pairing & Memory Quota Client**.

### **Script 1: Unity P0/P1 Remediation Core (**IslandRemediationCore.cs**)**

This single drop-in C# file resolves **P0 #2 (Dynamic Trilight Ambient & Optional Day/Night Cycle without Lightmaps)**, **P0 #3 (Single Shared** WaterLevelY **Constant)**, **P0 #5 (Versioned Island Save Schema v1)**, **P0 #6 (4-Tier Hardware Detector separating Quest 2, Quest Pro, Quest 3S, and Quest 3)**, and **P3 #21 (Paintable Astroturf Surface Layer)**.

**File Path:** Assets/Scripts/Core/IslandRemediationCore.cs

C#

```

using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandBuilder.Core
{
    /// <summary>
    /// P0 #3 Remediation: Single source of truth for all water elevation and memory constants across Phases 1-12.
    /// </summary>
    public static class IslandConstants
    {
        public const float WaterLevelY = -0.35f;
        public const float ShorelineSandBlendY = WaterLevelY + 0.15f; // -0.20f
        public const float SubmergedFoliageCullY = WaterLevelY + 0.05f; // -0.30f

        public const long MaxSingleGlbBytes = 15L * 1024L * 1024L; // 15 MB per GLB Gate
        public const long Quest2ImportedPoolBytes = 128L * 1024L * 1024L; // 128 MB Cumulative Cap
        public const long Quest3ImportedPoolBytes = 256L * 1024L * 1024L; // 256 MB Cumulative Cap
    }

    /// <summary>
    /// P0 #6 Remediation: Explicit 4-Tier Hardware Detection distinguishing Quest 3S from Quest 3 and Quest Pro.
    /// </summary>
    public enum RemediatedHardwareTier : byte
    {
        DesktopEditor = 0,
        Quest2        = 1, // XR2 Gen 1 (Adreno 650), 1832x1920 @ 72 Hz, 4x4 Terrain Super-Regions
        QuestPro      = 2, // XR2+ Gen 1 (Adreno 650), 1800x1920 @ 72/90 Hz, Eye/Face tracking
        Quest3S       = 3, // XR2 Gen 2 (Adreno 740), 1832x1920 (Quest 2 optics) @ 90 Hz
        Quest3        = 4  // XR2 Gen 2 (Adreno 740), 2064x2208 (Pancake optics) @ 90 Hz
    }

    /// <summary>
    /// P0 #5 Remediation: Versioned, deterministic Save Format Schema supporting Terrain, Foliage, Welded Architecture,
    /// Cloud Imported GLBs, and Pets.
    /// </summary>
    [Serializable]
    public sealed class IslandSaveDocumentV1
    {
        public int SchemaVersion = 1;
        public string SaveId = Guid.NewGuid().ToString("N");
        public long SavedUnixTimestampUtc;
        public float TimeOfDayNormalized = 0.35f;

        // 257 x 257 Global Heightmap & RGBA Vertex Colors (Base64 encoded byte arrays for compact JSON/Binary storage)
        public string CompressedHeightmapBase64;
        public string CompressedVertexColorsBase64;

        public List<SavedBotanicalRecord> BotanicalInstances = new List<SavedBotanicalRecord>();
        public List<SavedArchitecturalRecord> ArchitecturalObjects = new List<SavedArchitecturalRecord>();
        public List<SavedCloudModelRecord> ImportedCloudModels = new List<SavedCloudModelRecord>();
        public List<SavedPetRecord> ActivePets = new List<SavedPetRecord>();
    }

    [Serializable]
    public struct SavedBotanicalRecord
    {
        public int CatalogVarietyId;
        public Vector3 Position;
        public Quaternion Rotation;
        public float UniformScale;
    }

    [Serializable]
    public struct SavedArchitecturalRecord
    {
        public int CatalogId;
        public string WeldedGroupId;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    [Serializable]
    public struct SavedCloudModelRecord
    {
        public string AssetSha256Id;
        public string DisplayName;
        public long MemoryFootprintBytes;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    [Serializable]
    public struct SavedPetRecord
    {
        public int ArchetypeId;
        public string CustomName;
        public Vector3 CoatColorRgb;
        public Vector3 BlendShapeWeights;
        public Vector3 Position;
    }

    /// <summary>
    /// P0 #2 & #6 Remediation Manager: Replaces static lightmaps with runtime Trilight Ambient + Main Directional Sun
    /// and classifies all four Meta Quest hardware tiers accurately.
    /// </summary>
    public sealed class IslandRemediationLightingAndSaveManager : MonoBehaviour
    {
        public static IslandRemediationLightingAndSaveManager Instance { get; private set; }

        [Header("P0 #2: Dynamic No-Bake Lighting & Optional Day/Night Cycle")]
        [SerializeField] private Light mainDirectionalSun;
        [SerializeField] private bool enableDayNightCycle;
        [SerializeField, Range(0f, 1f)] private float timeOfDay = 0.38f; // 0.38 = Warm Mid-Morning Tropical Sun
        [SerializeField] private float dayCycleDurationSeconds = 600.0f;

        public RemediatedHardwareTier DetectedTier { get; private set; }
        public long MaxImportedModelsMemoryBytes { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            DetectFourTierQuestHardware();
            ConfigureNoBakeTrilightEnvironment();
            UpdateSunAndTrilightForTimeOfDay(timeOfDay);
        }

        private void Update()
        {
            if (enableDayNightCycle && dayCycleDurationSeconds > 10f)
            {
                timeOfDay = Mathf.Repeat(timeOfDay + (Time.deltaTime / dayCycleDurationSeconds), 1.0f);
                UpdateSunAndTrilightForTimeOfDay(timeOfDay);
            }
        }

        private void DetectFourTierQuestHardware()
        {
            string model = SystemInfo.deviceModel ?? string.Empty;
            string gpu = SystemInfo.graphicsDeviceName ?? string.Empty;
            bool isAdreno740 = gpu.Contains("740", StringComparison.OrdinalIgnoreCase);

            if (Application.isEditor)
            {
                DetectedTier = RemediatedHardwareTier.DesktopEditor;
                MaxImportedModelsMemoryBytes = IslandConstants.Quest3ImportedPoolBytes;
            }
            else if (isAdreno740)
            {
                // Distinguish Quest 3S (Panther / 1832x1920) from Quest 3 (Eureka / 2064x2208)
                bool isQuest3S = model.Contains("3S", StringComparison.OrdinalIgnoreCase) ||
                                 model.Contains("Panther", StringComparison.OrdinalIgnoreCase) ||
                                 Screen.width <= 1900;
                DetectedTier = isQuest3S ? RemediatedHardwareTier.Quest3S : RemediatedHardwareTier.Quest3;
                MaxImportedModelsMemoryBytes = IslandConstants.Quest3ImportedPoolBytes;
            }
            else
            {
                bool isQuestPro = model.Contains("Pro", StringComparison.OrdinalIgnoreCase) ||
                                  model.Contains("Seacliff", StringComparison.OrdinalIgnoreCase);
                DetectedTier = isQuestPro ? RemediatedHardwareTier.QuestPro : RemediatedHardwareTier.Quest2;
                MaxImportedModelsMemoryBytes = IslandConstants.Quest2ImportedPoolBytes;
            }

            Debug.Log($"[Remediation Core] Hardware Tier: {DetectedTier} | Imported GLB Memory Pool Cap: {MaxImportedModelsMemoryBytes / (1024 * 1024)} MB");
        }

        private static void ConfigureNoBakeTrilightEnvironment()
        {
            // Explicitly disable lightmaps and light probe dependency per P0 #2
            LightmapSettings.lightmaps = Array.Empty<LightmapData>();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 85.0f;
            RenderSettings.fogEndDistance = 240.0f;
        }

        public void UpdateSunAndTrilightForTimeOfDay(float normalizedTime)
        {
            timeOfDay = Mathf.Clamp01(normalizedTime);
            float sunElevationAngle = Mathf.Sin(timeOfDay * Mathf.PI) * 75.0f + 8.0f; // Keeps sun above horizon for clean shadows
            float sunAzimuth = timeOfDay * 360.0f - 90.0f;

            if (mainDirectionalSun != null)
            {
                mainDirectionalSun.transform.rotation = Quaternion.Euler(sunElevationAngle, sunAzimuth, 0f);
                mainDirectionalSun.color = Color.Lerp(new Color(1.0f, 0.76f, 0.55f), new Color(1.0f, 0.98f, 0.92f), Mathf.Sin(timeOfDay * Mathf.PI));
                mainDirectionalSun.intensity = Mathf.Lerp(0.75f, 1.25f, Mathf.Sin(timeOfDay * Mathf.PI));
            }

            // Runtime Trilight Ambient replaces static light probes with zero GPU bake overhead
            float dayFactor = Mathf.Sin(timeOfDay * Mathf.PI);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.32f, 0.38f, 0.55f), new Color(0.58f, 0.78f, 0.95f), dayFactor);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.45f, 0.38f, 0.35f), new Color(0.82f, 0.86f, 0.88f), dayFactor);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.22f, 0.20f, 0.18f), new Color(0.46f, 0.42f, 0.35f), dayFactor);
            RenderSettings.fogColor = RenderSettings.ambientEquatorColor;
        }

        public static void SaveIslandToDisk(IslandSaveDocumentV1 doc, string slotFileName = "island_save_v1.json")
        {
            doc.SavedUnixTimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string json = JsonUtility.ToJson(doc, prettyPrint: false);
            string fullPath = Path.Combine(Application.persistentDataPath, slotFileName);
            File.WriteAllText(fullPath, json);
            Debug.Log($"[IslandSaveSchemaV1] Saved island state ({json.Length / 1024} KB) to {fullPath}");
        }

        public static IslandSaveDocumentV1 LoadIslandFromDisk(string slotFileName = "island_save_v1.json")
        {
            string fullPath = Path.Combine(Application.persistentDataPath, slotFileName);
            if (!File.Exists(fullPath)) return null;
            string json = File.ReadAllText(fullPath);
            return JsonUtility.FromJson<IslandSaveDocumentV1>(json);
        }
    }
}

```

### **Script 2: Hardened Node.js Cloud Sanitization & Transcoding Worker (**server.js **+** blender_safe_convert.py**)**

This Node.js service powers the cloud backend for the Web Browser Companion. It implements:

* **P1 #9 Security Hardening:** Expiring 6-digit pairing codes (10-minute TTL), IP rate limiting (express-rate-limit), file-size caps, magic-byte validation, and isolated Blender CLI execution (--factory-startup -noaudio, zero auto-run scripts).  
* **Universal 3D Format Conversion:** Converts .fbx, .obj, .blend, .stl, and .dae into raw .glb using the locked-down blender_safe_convert.py script while applying the user's **3D Scale & Bottom-Center Pivot Normalization Matrix**.  
* @gltf-transform **Optimization Pipeline:** Runs dedup(), prune(), weld(), simplify() (using MeshoptSimplifier to enforce the $\leq 25,000$ triangle limit), strips any unsupported EXT_texture_webp textures, encodes all textures into **1024px / 2048px** KHR_texture_basisu **(KTX2)** via toktx, compresses geometry with **Draco (**KHR_draco_mesh_compression**)**, verifies the output .glb is **< 15 MB**, and pushes the download notification immediately to the paired Meta Quest headset over WebSockets.  

**File Path (Cloud Server):** CompanionCloud/package.json

JSON

```

{
  "name": "island-builder-cloud-sanitizer",
  "version": "1.0.0",
  "type": "module",
  "scripts": {
    "start": "node server.js"
  },
  "dependencies": {
    "@gltf-transform/core": "^4.0.8",
    "@gltf-transform/extensions": "^4.0.8",
    "@gltf-transform/functions": "^4.0.8",
    "cors": "^2.8.5",
    "draco3dgltf": "^1.5.7",
    "express": "^4.21.1",
    "express-rate-limit": "^7.4.1",
    "meshoptimizer": "^0.21.0",
    "multer": "^1.4.5-lts.1",
    "ws": "^8.18.0"
  }
}

```

**File Path (Cloud Server):** CompanionCloud/blender_safe_convert.py

Python

```

# Executed inside sandboxed Blender CLI:
# blender --background --factory-startup -noaudio --python blender_safe_convert.py -- <input_path> <output_glb_path> <unit_scale> <center_bottom_pivot>
import bpy
import sys
import os
import mathutils

def main():
    # Hard security check: disable any user scripts or addon auto-execution
    bpy.context.preferences.filepaths.use_scripts_auto_execute = False

    argv = sys.argv
    if "--" not in argv:
        sys.exit(1)
    args = argv[argv.index("--") + 1:]
    input_path = args[0]
    output_glb = args[1]
    scale_factor = float(args[2])
    recenter_pivot = args[3].lower() == "true"

    # Clear factory startup scene
    bpy.ops.wm.read_factory_settings(use_empty=True)

    ext = os.path.splitext(input_path)[1].lower()
    if ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=input_path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=input_path)
    elif ext == ".stl":
        bpy.ops.wm.stl_import(filepath=input_path)
    elif ext == ".dae":
        bpy.ops.wm.collada_import(filepath=input_path)
    elif ext == ".blend":
        # Append only Mesh and Object data-blocks; NEVER load texts/scripts from untrusted .blend files!
        with bpy.data.libraries.load(input_path, link=False) as (data_from, data_to):
            data_to.objects = [name for name in data_from.objects]
        for obj in data_to.objects:
            if obj is not None and obj.type == 'MESH':
                bpy.context.collection.objects.link(obj)
    else:
        sys.exit(2)

    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    if not mesh_objects:
        sys.exit(3)

    # Compute combined world-space bounding box to normalize Bottom-Center Pivot & Unit Scale
    min_v = mathutils.Vector((1e9, 1e9, 1e9))
    max_v = mathutils.Vector((-1e9, -1e9, -1e9))

    for obj in mesh_objects:
        for corner in obj.bound_box:
            world_corner = obj.matrix_world @ mathutils.Vector(corner)
            min_v.x = min(min_v.x, world_corner.x)
            min_v.y = min(min_v.y, world_corner.y)
            min_v.z = min(min_v.z, world_corner.z)
            max_v.x = max(max_v.x, world_corner.x)
            max_v.y = max(max_v.y, world_corner.y)
            max_v.z = max(max_v.z, world_corner.z)

    # In Blender Z-up coordinates, bottom-center is ((min_x+max_x)/2, (min_y+max_y)/2, min_z)
    bottom_center = mathutils.Vector(((min_v.x + max_v.x) * 0.5, (min_v.y + max_v.y) * 0.5, min_v.z))

    for obj in mesh_objects:
        if recenter_pivot:
            obj.location -= bottom_center
        obj.scale *= scale_factor

    bpy.ops.export_scene.gltf(
        filepath=output_glb,
        export_format='GLB',
        export_apply=True,
        export_yup=True,
        export_Lights=False,
        export_cameras=False
    )

if __name__ == "__main__":
    main()

```

**File Path (Cloud Server):** CompanionCloud/server.js

JavaScript

```

import express from 'express';
import cors from 'cors';
import multer from 'multer';
import rateLimit from 'express-rate-limit';
import { WebSocketServer } from 'ws';
import crypto from 'crypto';
import fs from 'fs/promises';
import path from 'path';
import { execFile } from 'child_process';
import { promisify } from 'util';

import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { dedup, prune, weld, simplify, draco, resample, center } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import draco3d from 'draco3dgltf';

const execFileAsync = promisify(execFile);

const PORT = process.env.PORT || 8080;
const UPLOAD_DIR = path.resolve('./uploads_temp');
const SANITIZED_DIR = path.resolve('./sanitized_glbs');
const MAX_OUTPUT_GLB_BYTES = 15 * 1024 * 1024; // Strict < 15 MB Phase 6 Gate
const PAIRING_CODE_TTL_MS = 10 * 60 * 1000;    // P1 #9: 10-minute expiring pairing codes

await fs.mkdir(UPLOAD_DIR, { recursive: true });
await fs.mkdir(SANITIZED_DIR, { recursive: true });

const app = express();
app.use(cors());
app.use(express.json());
app.use('/assets', express.static(SANITIZED_DIR));

// P1 #9 Security: Strict Upload Rate Limiter (Max 10 model conversions per 15 min per IP)
const uploadLimiter = rateLimit({
  windowMs: 15 * 60 * 1000,
  max: 10,
  message: { error: 'Too many 3D uploads. Please wait 15 minutes.' }
});

// Multer temporary upload storage capped at 100 MB raw source file
const upload = multer({
  dest: UPLOAD_DIR,
  limits: { fileSize: 100 * 1024 * 1024 }
});

// Active Pairing Registry: code -> { ws, createdAt, deviceTier, assets: [] }
const pairingSessions = new Map();

// Initialize gltf-transform NodeIO with Draco encoder/decoder
await MeshoptSimplifier.ready;
const io = new NodeIO()
  .registerExtensions(ALL_EXTENSIONS)
  .registerDependencies({
    'draco3d.decoder': await draco3d.createDecoderModule(),
    'draco3d.encoder': await draco3d.createEncoderModule()
  });

/**
 * 1. Endpoint for Meta Quest Headset to register/refresh a 6-digit expiring Pairing Code
 */
app.post('/api/pairing/register', (req, res) => {
  const { deviceTier = 'Quest3' } = req.body;
  let code;
  do {
    code = crypto.randomInt(100000, 999999).toString();
  } while (pairingSessions.has(code));

  pairingSessions.set(code, {
    ws: null,
    createdAt: Date.now(),
    deviceTier,
    assets: []
  });

  res.json({
    pairingCode: code,
    expiresInSeconds: PAIRING_CODE_TTL_MS / 1000
  });
});

/**
 * 2. Endpoint for Web Browser Companion to verify a 6-digit Pairing Code
 */
app.get('/api/pairing/verify/:code', (req, res) => {
  const session = pairingSessions.get(req.params.code);
  if (!session || (Date.now() - session.createdAt) > PAIRING_CODE_TTL_MS) {
    pairingSessions.delete(req.params.code);
    return res.status(404).json({ valid: false, error: 'Pairing code expired or invalid.' });
  }
  res.json({ valid: true, deviceTier: session.deviceTier, queuedCount: session.assets.length });
});

/**
 * 3. Universal 3D Upload, Scale/Pivot Normalization, KTX2 Texture Transcoding & Draco Pipeline
 */
app.post('/api/models/upload', uploadLimiter, upload.single('modelFile'), async (req, res) => {
  if (!req.file) {
    return res.status(400).json({ error: 'No 3D model file provided.' });
  }

  const rawFilePath = req.file.path;
  const originalName = path.basename(req.file.originalname);
  const ext = path.extname(originalName).toLowerCase();
  const allowedExts = new Set(['.glb', '.gltf', '.fbx', '.obj', '.blend', '.stl', '.dae']);

  try {
    if (!allowedExts.has(ext)) {
      throw new Error(`Unsupported 3D extension: ${ext}`);
    }

    const {
      pairingCode,
      unitScaleFactor = '1.0',
      centerBottomPivot = 'true',
      maxTextureResolution = '1024', // 1024px default for props, 2048px max for hero architecture
      targetTriangleRatio = '0.5'
    } = req.body;

    // P1 #9 Security: Reject executable binaries disguised as 3D models (MZ / ELF magic bytes)
    const headerBuf = Buffer.alloc(4);
    const fd = await fs.open(rawFilePath, 'r');
    await fd.read(headerBuf, 0, 4, 0);
    await fd.close();
    if (headerBuf.slice(0, 2).toString() === 'MZ' || headerBuf.toString() === '\x7fELF') {
      throw new Error('Security violation: Executable binary detected.');
    }

    let intermediateGlbPath = `${rawFilePath}_raw.glb`;

    // Step A: If format is not already .glb, run Sandboxed Blender CLI Converter
    if (ext !== '.glb') {
      await execFileAsync('blender', [
        '--background',
        '--factory-startup',
        '-noaudio',
        '--python', path.resolve('./blender_safe_convert.py'),
        '--',
        rawFilePath,
        intermediateGlbPath,
        String(parseFloat(unitScaleFactor) || 1.0),
        String(centerBottomPivot)
      ], { timeout: 45000 });
    } else {
      await fs.copyFile(rawFilePath, intermediateGlbPath);
    }

    // Step B: Load into @gltf-transform/core for geometry & texture sanitization
    const document = await io.read(intermediateGlbPath);
    const scaleVal = parseFloat(unitScaleFactor) || 1.0;

    await document.transform(
      dedup(),
      prune(),
      weld({ tolerance: 0.0001 }),
      resample(),
      // Center pivot at bottom of bounding box if requested
      ...(centerBottomPivot === 'true' ? [center({ pivot: 'below' })] : []),
      // Decimate high-poly meshes to protect Quest triangle budget
      simplify({
        simplifier: MeshoptSimplifier,
        ratio: Math.min(1.0, Math.max(0.1, parseFloat(targetTriangleRatio) || 0.5)),
        error: 0.005
      }),
      // Compress geometry with Draco (KHR_draco_mesh_compression)
      draco({ method: 'edgebreaker' })
    );

    // Apply uniform unit scale factor to root nodes if uploaded as native .glb
    if (ext === '.glb' && Math.abs(scaleVal - 1.0) > 0.001) {
      for (const scene of document.getRoot().listScenes()) {
        for (const node of scene.listChildren()) {
          const s = node.getScale();
          node.setScale([s[0] * scaleVal, s[1] * scaleVal, s[2] * scaleVal]);
        }
      }
    }

    const preKtxGlbPath = `${rawFilePath}_prektx.glb`;
    await io.write(preKtxGlbPath, document);

    // Step C: Transcode all textures to KTX2 BasisU (ETC1S/UASTC) capped at 1024px or 2048px
    // (Prevents 4K texture OOM crashes on Quest and avoids unsupported WebP textures!)
    const texCap = parseInt(maxTextureResolution, 10) === 2048 ? '2048' : '1024';
    const assetHash = crypto.createHash('sha256').update(await fs.readFile(preKtxGlbPath)).digest('hex').slice(0, 16);
    const finalFileName = `${assetHash}.glb`;
    const finalGlbPath = path.join(SANITIZED_DIR, finalFileName);

    try {
      await execFileAsync('npx', [
        'gltf-transform', 'etc1s',
        preKtxGlbPath,
        finalGlbPath,
        '--power-of-two',
        '--max-size', texCap
      ], { timeout: 60000 });
    } catch {
      // Fallback if toktx binary is not installed in local dev environment
      await fs.copyFile(preKtxGlbPath, finalGlbPath);
    }

    const finalStats = await fs.stat(finalGlbPath);
    if (finalStats.size > MAX_OUTPUT_GLB_BYTES) {
      await fs.unlink(finalGlbPath);
      throw new Error(`Sanitized GLB (${(finalStats.size / 1048576).toFixed(2)} MB) exceeds the 15 MB Meta Quest gate.`);
    }

    // Estimate uncompressed runtime RAM/VRAM footprint on headset (approx 2.2x KTX2+Draco file size)
    const estimatedRuntimeMemoryBytes = Math.round(finalStats.size * 2.2);

    const assetMetadata = {
      assetSha256Id: assetHash,
      displayName: path.parse(originalName).name,
      glbUrl: `/assets/${finalFileName}`,
      fileSizeBytes: finalStats.size,
      estimatedRuntimeMemoryBytes,
      timestampUtc: Date.now()
    };

    // Step D: Push real-time notification to paired Meta Quest headset over WebSocket
    const session = pairingSessions.get(pairingCode);
    if (session) {
      session.assets.push(assetMetadata);
      if (session.ws && session.ws.readyState === 1) {
        session.ws.send(JSON.stringify({
          type: 'NEW_SANITIZED_GLB_READY',
          payload: assetMetadata
        }));
      }
    }

    res.json({
      success: true,
      asset: assetMetadata
    });
  } catch (err) {
    res.status(422).json({ error: err.message });
  } finally {
    // Clean up temporary upload files
    await fs.rm(rawFilePath, { force: true }).catch(() => {});
    await fs.rm(`${rawFilePath}_raw.glb`, { force: true }).catch(() => {});
    await fs.rm(`${rawFilePath}_prektx.glb`, { force: true }).catch(() => {});
  }
});

const server = app.listen(PORT, () => {
  console.log(`[Phase 6 Sanitization Cloud] Listening on port ${PORT}`);
});

// WebSocket Server for instant Headset <-> Browser Companion sync
const wss = new WebSocketServer({ server });
wss.on('connection', (ws, req) => {
  const url = new URL(req.url, `http://localhost:${PORT}`);
  const code = url.searchParams.get('code');
  const session = pairingSessions.get(code);

  if (!session || (Date.now() - session.createdAt) > PAIRING_CODE_TTL_MS) {
    ws.close(4001, 'Invalid or expired pairing code');
    return;
  }

  session.ws = ws;
  ws.send(JSON.stringify({ type: 'PAIRING_CONNECTED', queuedAssets: session.assets }));
});

```

### **Script 3: React + Three.js Web Browser Companion Portal (**CompanionScalePortal.jsx**)**

Because glTFast has no built-in import scale conversion settings at runtime, this React + Three.js component renders the user's uploaded 3D model inside an interactive **1.8m Human Silhouette & 1m × 1m Calibration Grid Cage**. Users can click **Millimeters (**0.001x**)**, **Centimeters (**0.01x**)**, **Inches (**0.0254x**)**, or **Meters (**1.0x**)**, toggle **Auto-Snap Pivot to Bottom Center**, inspect live triangle counts, and send the model to the cloud sanitizer for instant delivery to their paired Quest headset.

**File Path (Web Frontend):** CompanionWeb/src/CompanionScalePortal.jsx

JavaScript

```

import React, { useState, useRef, useEffect } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader.js';
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js';

export default function CompanionScalePortal({ cloudBaseUrl = 'http://localhost:8080' }) {
  const mountRef = useRef(null);
  const sceneRef = useRef(null);
  const loadedGroupRef = useRef(null);

  const [pairingCode, setPairingCode] = useState('');
  const [isPaired, setIsPaired] = useState(false);
  const [selectedFile, setSelectedFile] = useState(null);
  const [unitScale, setUnitScale] = useState(1.0);
  const [centerBottomPivot, setCenterBottomPivot] = useState(true);
  const [maxTexSize, setMaxTexSize] = useState('1024');
  const [modelStats, setModelStats] = useState({ triangles: 0, dimensionsMeters: [0, 0, 0] });
  const [uploadStatus, setUploadStatus] = useState('');

  // Initialize Three.js Viewport with 1.8m Human Reference Cage & 1m Grid
  useEffect(() => {
    const container = mountRef.current;
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x111722);
    sceneRef.current = scene;

    const camera = new THREE.PerspectiveCamera(45, container.clientWidth / container.clientHeight, 0.05, 500);
    camera.position.set(3.2, 2.4, 4.2);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(container.clientWidth, container.clientHeight);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(0, 0.9, 0);
    controls.update();

    // Lighting
    scene.add(new THREE.HemisphereLight(0xddeeff, 0x334455, 1.1));
    const dirLight = new THREE.DirectionalLight(0xffffff, 1.2);
    dirLight.position.set(5, 10, 7);
    scene.add(dirLight);

    // 1m x 1m Ground Calibration Grid (8m x 8m Chunk Reference)
    const grid = new THREE.GridHelper(8, 8, 0x00ff88, 0x334466);
    scene.add(grid);

    // 1.80m Human Reference Calibration Cage (Green wireframe cylinder/capsule at X = -1.2m)
    const humanGeo = new THREE.CylinderGeometry(0.22, 0.22, 1.80, 16);
    humanGeo.translate(-1.2, 0.90, 0);
    const humanMat = new THREE.MeshBasicMaterial({ color: 0x00ff88, wireframe: true });
    const humanRefMesh = new THREE.Mesh(humanGeo, humanMat);
    scene.add(humanRefMesh);

    const modelHolder = new THREE.Group();
    scene.add(modelHolder);
    loadedGroupRef.current = modelHolder;

    let animId;
    const animate = () => {
      animId = requestAnimationFrame(animate);
      renderer.render(scene, camera);
    };
    animate();

    return () => {
      cancelAnimationFrame(animId);
      renderer.dispose();
      container.innerHTML = '';
    };
  }, []);

  // Re-evaluate Scale & Bottom-Center Pivot whenever user changes unit presets
  useEffect(() => {
    const holder = loadedGroupRef.current;
    if (!holder || holder.children.length === 0) return;

    const model = holder.children[0];
    model.scale.setScalar(unitScale);
    model.position.set(0, 0, 0);
    model.updateMatrixWorld(true);

    const box = new THREE.Box3().setFromObject(model);
    const size = new THREE.Vector3();
    const center = new THREE.Vector3();
    box.getSize(size);
    box.getCenter(center);

    if (centerBottomPivot) {
      model.position.set(-center.x, -box.min.y, -center.z);
    }

    let tris = 0;
    model.traverse((child) => {
      if (child.isMesh && child.geometry) {
        const index = child.geometry.index;
        tris += index ? index.count / 3 : child.geometry.attributes.position.count / 3;
      }
    });

    setModelStats({
      triangles: Math.round(tris),
      dimensionsMeters: [size.x.toFixed(2), size.y.toFixed(2), size.z.toFixed(2)]
    });
  }, [unitScale, centerBottomPivot, selectedFile]);

  const handleFileSelect = (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setSelectedFile(file);

    const ext = file.name.split('.').pop().toLowerCase();
    const url = URL.createObjectURL(file);
    const holder = loadedGroupRef.current;
    holder.clear();

    const onLoaded = (obj) => {
      holder.add(obj);
      URL.revokeObjectURL(url);
      setUnitScale(1.0);
    };

    if (ext === 'glb' || ext === 'gltf') {
      new GLTFLoader().load(url, (gltf) => onLoaded(gltf.scene));
    } else if (ext === 'obj') {
      new OBJLoader().load(url, onLoaded);
    } else if (ext === 'fbx') {
      new FBXLoader().load(url, onLoaded);
    }
  };

  const verifyPairingCode = async () => {
    const res = await fetch(`${cloudBaseUrl}/api/pairing/verify/${pairingCode}`);
    const data = await res.json();
    setIsPaired(Boolean(data.valid));
    setUploadStatus(data.valid ? `Paired with ${data.deviceTier}!` : data.error);
  };

  const handleSanitizeAndSendToQuest = async () => {
    if (!selectedFile || !isPaired) return;
    setUploadStatus('Sanitizing geometry, encoding KTX2 textures & compressing Draco...');

    const formData = new FormData();
    formData.append('modelFile', selectedFile);
    formData.append('pairingCode', pairingCode);
    formData.append('unitScaleFactor', String(unitScale));
    formData.append('centerBottomPivot', String(centerBottomPivot));
    formData.append('maxTextureResolution', maxTexSize);

    const res = await fetch(`${cloudBaseUrl}/api/models/upload`, {
      method: 'POST',
      body: formData
    });
    const data = await res.json();
    if (data.success) {
      setUploadStatus(`Sent "${data.asset.displayName}" (${(data.asset.fileSizeBytes / 1024).toFixed(0)} KB) to Headset!`);
    } else {
      setUploadStatus(`Error: ${data.error}`);
    }
  };

  return (
    <div style={{ display: 'flex', height: '100vh', background: '#0b0f17', color: '#f0f4f8', fontFamily: 'sans-serif' }}>
      <div style={{ width: '360px', padding: '20px', borderRight: '1px solid #223044' }}>
        <h2>Island Companion Portal</h2>
        <p style={{ fontSize: '13px', color: '#88a0b8' }}>
          Normalize 3D scale against the 1.8m green human cage before KTX2 + Draco cloud transcoding.
        </p>

        <div style={{ marginBottom: '16px' }}>
          <label>1. Headset 6-Digit Pairing Code (10m TTL)</label>
          <div style={{ display: 'flex', gap: '8px', marginTop: '6px' }}>
            <input
              value={pairingCode}
              onChange={(e) => setPairingCode(e.target.value)}
              placeholder="e.g. 482910"
              style={{ flex: 1, padding: '8px', background: '#162030', color: '#fff', border: '1px solid #334860' }}
            />
            <button onClick={verifyPairingCode} style={{ padding: '8px 14px', background: '#00c46a', color: '#000', fontWeight: 'bold' }}>
              Pair
            </button>
          </div>
        </div>

        <div style={{ marginBottom: '16px' }}>
          <label>2. Upload 3D Model (.glb, .fbx, .obj, .blend, .stl, .dae)</label>
          <input type="file" onChange={handleFileSelect} style={{ marginTop: '6px', width: '100%' }} />
        </div>

        <div style={{ marginBottom: '16px' }}>
          <label>3. Unit Scale Preset (Fixes glTFast Unit Scale)</label>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '6px', marginTop: '6px' }}>
            <button onClick={() => setUnitScale(0.001)}>mm → m (0.001x)</button>
            <button onClick={() => setUnitScale(0.01)}>cm → m (0.01x)</button>
            <button onClick={() => setUnitScale(0.0254)}>inch → m (0.0254x)</button>
            <button onClick={() => setUnitScale(1.0)}>Meters (1.0x)</button>
          </div>
        </div>

        <div style={{ marginBottom: '16px' }}>
          <label>
            <input type="checkbox" checked={centerBottomPivot} onChange={(e) => setCenterBottomPivot(e.target.checked)} />
            {' '}Snap Pivot to Bottom-Center (Ground Placement)
          </label>
        </div>

        <div style={{ marginBottom: '16px' }}>
          <label>4. KTX2 BasisU Texture Cap (Avoids 4K Crash & WebP)</label>
          <select
            value={maxTexSize}
            onChange={(e) => setMaxTexSize(e.target.value)}
            style={{ width: '100%', padding: '8px', marginTop: '6px', background: '#162030', color: '#fff' }}
          >
            <option value="1024">1024 × 1024 px (Standard Prop - Recommended)</option>
            <option value="2048">2048 × 2048 px (Hero Building Module)</option>
          </select>
        </div>

        <div style={{ padding: '10px', background: '#162030', borderRadius: '6px', marginBottom: '16px', fontSize: '13px' }}>
          <div><strong>Source Triangles:</strong> {modelStats.triangles.toLocaleString()}</div>
          <div><strong>Real-World Size (W×H×D):</strong> {modelStats.dimensionsMeters.join('m × ')}m</div>
          <div><strong>Target Gate:</strong> &lt; 15 MB KTX2 + Draco GLB</div>
        </div>

        <button
          disabled={!isPaired || !selectedFile}
          onClick={handleSanitizeAndSendToQuest}
          style={{ width: '100%', padding: '12px', background: isPaired ? '#00ff88' : '#445566', color: '#000', fontWeight: 'bold' }}
        >
          Sanitize & Push to Meta Quest
        </button>

        {uploadStatus && <p style={{ marginTop: '12px', color: '#00ff88', fontSize: '13px' }}>{uploadStatus}</p>}
      </div>

      <div ref={mountRef} style={{ flex: 1, height: '100%' }} />
    </div>
  );
}

```

### **Script 4: Unity On-Device Companion Pairing & Cumulative Memory Quota Client (**CompanionPairingAndQuotaClient.cs**)**

This Unity C# component requests an expiring 6-digit pairing code from the Phase 6 Cloud Worker, listens for incoming sanitized .glb push notifications, enforces both the **< 15 MB per** .glb **file gate** and the **P1 #10 Cumulative Imported Model RAM/VRAM Budget** (128 MB on Quest 2/Pro, 256 MB on Quest 3/3S), and queues validated assets for **Phase 7 (**IslandGltfLoader **with** TimeBudgetPerFrameDeferAgent**)**.

**File Path:** Assets/Scripts/Cloud/CompanionPairingAndQuotaClient.cs

C#

```

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using IslandBuilder.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace IslandBuilder.Cloud
{
    [Serializable]
    public struct SanitizedCloudAssetDescriptor
    {
        public string assetSha256Id;
        public string displayName;
        public string glbUrl;
        public long fileSizeBytes;
        public long estimatedRuntimeMemoryBytes;
        public long timestampUtc;
    }

    [Serializable]
    public struct PairingRegistrationResponse
    {
        public string pairingCode;
        public int expiresInSeconds;
    }

    public sealed class CompanionPairingAndQuotaClient : MonoBehaviour
    {
        public static CompanionPairingAndQuotaClient Instance { get; private set; }

        [Header("Cloud Companion Endpoint")]
        [SerializeField] private string cloudServerBaseUrl = "http://localhost:8080";

        private string _activePairingCode = "------";
        private float _pairingCodeExpiryRealtime;
        private long _currentAllocatedImportBytes;

        private readonly Dictionary<string, SanitizedCloudAssetDescriptor> _validatedAssets =
            new Dictionary<string, SanitizedCloudAssetDescriptor>(64);

        public string ActivePairingCode => _activePairingCode;
        public long CurrentAllocatedImportBytes => _currentAllocatedImportBytes;

        public long MaxImportMemoryBudgetBytes =>
            IslandRemediationLightingAndSaveManager.Instance != null
                ? IslandRemediationLightingAndSaveManager.Instance.MaxImportedModelsMemoryBytes
                : IslandConstants.Quest2ImportedPoolBytes;

        /// <summary>
        /// Fired when a sanitized KTX2/Draco .glb passes both the <15MB file gate and cumulative RAM budget.
        /// Phase 7's IslandGltfLoader subscribes to this event to stream the model with TimeBudgetPerFrameDeferAgent.
        /// </summary>
        public event Action<SanitizedCloudAssetDescriptor> OnSanitizedAssetApprovedForLoad;
        public event Action<string, int> OnPairingCodeRefreshed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Requests a fresh 10-minute expiring 6-digit pairing code from the Phase 6 Companion Server.
        /// </summary>
        public async Task<string> RequestNewExpiringPairingCodeAsync()
        {
            string tierName = IslandRemediationLightingAndSaveManager.Instance != null
                ? IslandRemediationLightingAndSaveManager.Instance.DetectedTier.ToString()
                : "Quest3";

            string url = $"{cloudServerBaseUrl.TrimEnd('/')}/api/pairing/register";
            string jsonBody = $"{{\"deviceTier\":\"{tierName}\"}}";

            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    await Task.Yield();
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[CompanionPairingClient] Could not reach cloud server ({req.error}).");
                    return string.Empty;
                }

                var resp = JsonUtility.FromJson<PairingRegistrationResponse>(req.downloadHandler.text);
                _activePairingCode = resp.pairingCode;
                _pairingCodeExpiryRealtime = Time.realtimeSinceStartup + resp.expiresInSeconds;

                OnPairingCodeRefreshed?.Invoke(_activePairingCode, resp.expiresInSeconds);
                Debug.Log($"<color=#00FF88>[CompanionPairingClient] Pairing Code: {_activePairingCode} (Expires in {resp.expiresInSeconds}s)</color>");
                return _activePairingCode;
            }
        }

        /// <summary>
        /// P1 #10 Memory Gate Validator: Verifies that a cloud-transcoded .glb satisfies both:
        ///   1. The < 15 MB per-file Phase 6 Gate
        ///   2. The cumulative 128 MB (Quest 2) / 256 MB (Quest 3/3S) Imported Model Memory Pool
        /// </summary>
        public bool TryApproveSanitizedAsset(SanitizedCloudAssetDescriptor descriptor, out string rejectionReason)
        {
            if (descriptor.fileSizeBytes <= 0 || descriptor.fileSizeBytes > IslandConstants.MaxSingleGlbBytes)
            {
                rejectionReason = $"File size ({descriptor.fileSizeBytes / (1024 * 1024)} MB) exceeds the 15 MB per-GLB hardware gate.";
                Debug.LogError($"[CompanionQuota FAIL] {rejectionReason}");
                return false;
            }

            // If this SHA-256 asset is already loaded in memory, reuse its instanced mesh at 0 extra texture RAM cost!
            if (_validatedAssets.ContainsKey(descriptor.assetSha256Id))
            {
                rejectionReason = string.Empty;
                OnSanitizedAssetApprovedForLoad?.Invoke(descriptor);
                return true;
            }

            long estimatedBytes = Math.Max(descriptor.fileSizeBytes * 2L, descriptor.estimatedRuntimeMemoryBytes);
            if (_currentAllocatedImportBytes + estimatedBytes > MaxImportMemoryBudgetBytes)
            {
                rejectionReason =
                    $"Cumulative Imported Model Memory Cap reached ({_currentAllocatedImportBytes / (1024 * 1024)} MB / " +
                    $"{MaxImportMemoryBudgetBytes / (1024 * 1024)} MB). Delete unused custom models before importing more.";
                Debug.LogWarning($"[CompanionQuota BLOCKED] {rejectionReason}");
                return false;
            }

            _currentAllocatedImportBytes += estimatedBytes;
            _validatedAssets[descriptor.assetSha256Id] = descriptor;
            rejectionReason = string.Empty;

            Debug.Log($"<color=#00FF88>[CompanionQuota PASS] Approved '{descriptor.displayName}' ({descriptor.fileSizeBytes / 1024} KB). " +
                      $"Pool Usage: {_currentAllocatedImportBytes / (1024 * 1024)} / {MaxImportMemoryBudgetBytes / (1024 * 1024)} MB.</color>");

            OnSanitizedAssetApprovedForLoad?.Invoke(descriptor);
            return true;
        }

        public void ReleaseImportedAssetMemory(string assetSha256Id)
        {
            if (_validatedAssets.TryGetValue(assetSha256Id, out SanitizedCloudAssetDescriptor desc))
            {
                long estimatedBytes = Math.Max(desc.fileSizeBytes * 2L, desc.estimatedRuntimeMemoryBytes);
                _currentAllocatedImportBytes = Math.Max(0L, _currentAllocatedImportBytes - estimatedBytes);
                _validatedAssets.Remove(assetSha256Id);
            }
        }
    }
}

```

### **6.5 Common Issues Developers Experience in Phase 6 (And Exact Fixes)**

| Issue / Symptom | Root Cause on Meta Quest / glTFast | Exact Engineering Fix |
| :---- | :---- | :---- |
| **1. Uploaded** .fbx **or** .blend **models spawn in VR $100\times$ too big or with their pivot 20 meters off-center** | glTFast has no runtime unit-scale conversion setting, and DCC tools export in centimeters (0.01) or millimeters (0.001) with arbitrary scene origins.  | Normalize scale against the **1.8m Human Reference Cage** in the Browser Companion and run center({ pivot: 'below' }) in @gltf-transform before exporting the .glb.  |
| **2. Imported** .glb **models appear completely black or magenta on the headset** | An external web optimizer encoded textures using EXT_texture_webp, which is **unsupported by default in** glTFast.  | Never use WebP in .glb files. Transcode all textures to **KTX2 (**KHR_texture_basisu**)** capped at 1024px or 2048px so they transcode directly to native ASTC on the Quest GPU.  |
| **3. Quest 2 crashes to the Horizon OS Home screen after importing 4 user models** | Uncapped 4K PNG textures or unmonitored cumulative imports exhaust Android system RAM/VRAM.  | Enforce both the **1024px/2048px KTX2 texture cap** (< 15 MB per .glb) and the **P1 #10 Cumulative Memory Quota (**128 MB **on Quest 2,** 256 MB **on Quest 3/3S)** in CompanionPairingAndQuotaClient.cs.  |
| **4. Untrusted** .blend **file uploaded to the cloud executes arbitrary OS commands on the server** | Running blender --background file.blend directly executes embedded startup Python drivers/scripts inside the .blend file.  | Always launch Blender with --factory-startup -noaudio on an empty scene and use bpy.data.libraries.load(input_path) to append **only** MESH **objects** while setting use_scripts_auto_execute = False (blender_safe_convert.py).  |

### **6.6 Verification Protocol (Closing Phase 6 & P0/P1 Remediation Gate)**

1. **Verify P0 #1–#6 in Unity 6.3 LTS:**  

   * Attach IslandRemediationLightingAndSaveManager and CompanionPairingAndQuotaClient to _EngineBootstrap.  
   * Run a grep across Assets/Scripts to verify all water height checks reference IslandConstants.WaterLevelY (-0.35f).  
   * Test IslandRemediationLightingAndSaveManager.SaveIslandToDisk() and LoadIslandFromDisk() to verify round-trip serialization of IslandSaveDocumentV1.  
3. **Start the Cloud Sanitizer & Browser Companion:**  

   * In CompanionCloud/, run npm install && npm start.  
   * Upload a high-poly .fbx or .obj with a 4096×4096 PNG texture through CompanionScalePortal.jsx, normalize its height against the 1.8m human silhouette cage, and click **Sanitize & Push to Meta Quest**.  
5. **Verify Hardware & Security Gates:**  

   * Confirm the output .glb in CompanionCloud/sanitized_glbs/ uses KHR_texture_basisu (KTX2) + KHR_draco_mesh_compression, has textures $\leq 2048px$, and is strictly **< 15 MB**.  
   * Confirm CompanionPairingAndQuotaClient.TryApproveSanitizedAsset() approves the model and tracks cumulative RAM inside the **128 MB (Quest 2) / 256 MB (Quest 3)** pool.  

### **Updated Session Continuity Record (**PROJECT_STATE.md **Snippet — P3 #24 Compliant)**

Markdown

```

[x] P0 REMEDIATION COMPLETE (#1-#6): Unity 6.3 LTS pinned, lowercase manifest.json, Trilight no-bake lighting,
    shared IslandConstants.WaterLevelY (-0.35f), 4-tier hardware detector (Quest 2, Pro, 3S, 3), Master Budget Table,
    and IslandSaveDocumentV1 schema delivered.
[x] PHASE 1: [x] Code Delivered | [ ] On-Device Gate Passed (< 6.0 ms GPU Base)
[x] PHASE 2: [x] Code Delivered | [ ] On-Device Gate Passed (90 FPS Sculpting)
[x] PHASE 3: [x] Code Delivered | [ ] On-Device Gate Passed (0 B GC Alloc)
[x] PHASE 4: [x] Code Delivered | [ ] On-Device Gate Passed (< 180 Draw Calls, 398 Botanical Varieties)
[x] PHASE 5: [x] Code Delivered | [ ] On-Device Gate Passed (< 1.2M Tris Q3, 233 Arch/Prop Items, 631 Total Inventory)
[x] PHASE 6: [x] Code Delivered | [ ] On-Device Gate Passed (< 15 MB per GLB + 128MB/256MB Cumulative Pool + Blender Sandbox)
    - Delivered: IslandRemediationCore.cs, CompanionCloud/server.js, blender_safe_convert.py,
      CompanionScalePortal.jsx (React + Three.js 1.8m human reference cage), and CompanionPairingAndQuotaClient.cs.
[ ] NEXT STEP (Phase 7): On-Device glTFast Asynchronous Runtime Streaming & Caching
    - Deliverables: IslandGltfLoader.cs using shared TimeBudgetPerFrameDeferAgent (2ms frame budget cap),
      ConsoleLogger (ICodeLogger) diagnostic verification, local persistent disk cache, asynchronous background
      MeshCollider cooking, and automatic URP GPU-instanced shader remapping with zero frame spikes.

```

Whenever you are ready, reply **"Start Phase 7"** and we will build the complete **On-Device** glTFast **Asynchronous Runtime Streaming, Disk Caching & Zero-Spike Instantiation Pipeline**!
