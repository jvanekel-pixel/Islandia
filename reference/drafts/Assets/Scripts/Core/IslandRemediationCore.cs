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
