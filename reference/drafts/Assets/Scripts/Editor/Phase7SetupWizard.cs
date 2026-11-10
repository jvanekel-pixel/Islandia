#if UNITY_EDITOR
using System.IO;
using IslandBuilder.Architecture;
using IslandBuilder.Cloud;
using IslandBuilder.Core;
using UnityEditor;
using UnityEngine;

namespace IslandBuilder.EditorTools
{
    public static class Phase7SetupWizard
    {
        private const string LinkXmlPath = "Assets/link.xml";

        [MenuItem("IslandBuilder/Phase 7/1. Wire glTFast Async Loader, Disk Cache & link.xml", false, 50)]
        public static void SetupPhase7GltfStreamingSuite()
        {
            EnsureLinkXmlPreservationFile();

            Shader gltfShader = Shader.Find("IslandBuilder/GltfSinglePassLit");
            if (gltfShader == null)
            {
                Debug.LogError("[Phase 7 Wizard] Could not find GltfSinglePassLit.shader!");
                return;
            }

            GameObject cloudRoot = GameObject.Find("_Phase6_7_Cloud_Streaming_System");
            if (cloudRoot == null) cloudRoot = new GameObject("_Phase6_7_Cloud_Streaming_System");

            if (cloudRoot.GetComponent<IslandRemediationLightingAndSaveManager>() == null)
                cloudRoot.AddComponent<IslandRemediationLightingAndSaveManager>();

            if (cloudRoot.GetComponent<CompanionPairingAndQuotaClient>() == null)
                cloudRoot.AddComponent<CompanionPairingAndQuotaClient>();

            if (cloudRoot.GetComponent<GltfPersistentDiskCache>() == null)
                cloudRoot.AddComponent<GltfPersistentDiskCache>();

            if (cloudRoot.GetComponent<IslandGltfLoader>() == null)
                cloudRoot.AddComponent<IslandGltfLoader>();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 7 Wizard] Wired GltfPersistentDiskCache, IslandGltfLoader (TimeBudgetPerFrameDeferAgent), and verified Assets/link.xml!</color>");
        }

        [MenuItem("IslandBuilder/Phase 7/2. Run Round-Trip Async .glb Stream Stress Test (Play Mode)", false, 51)]
        public static async void RunPlayModeGlbStreamingTest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Phase 7 Stress Test] Please enter Play Mode first, then click this menu item.");
                return;
            }

            if (IslandGltfExporter.Instance == null || IslandGltfLoader.Instance == null)
            {
                Debug.LogError("[Phase 7 Stress Test] Ensure Phase 5 and Phase 7 Setup Wizards have been run.");
                return;
            }

            // Create a temporary procedural test sculpture to export as .glb and stream back asynchronously
            GameObject tempProp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tempProp.name = "SelfTest_Custom_Sculpture";
            tempProp.transform.position = new Vector3(0f, 1.0f, 4.0f);

            string exportedGlbPath = await IslandGltfExporter.Instance.ExportHierarchyToGlbAsync(
                new[] { tempProp }, "Phase7_SelfTest_Asset");
            Object.Destroy(tempProp);

            if (string.IsNullOrEmpty(exportedGlbPath) || !File.Exists(exportedGlbPath))
            {
                Debug.LogError("[Phase 7 Stress Test] Failed to export self-test .glb.");
                return;
            }

            long sizeBytes = new FileInfo(exportedGlbPath).Length;
            string testSha = "selftest_sha256_001";

            // Copy into GltfPersistentDiskCache and trigger CompanionPairingAndQuotaClient approval
            string cachedPath = GltfPersistentDiskCache.Instance.GetCachedGlbFilePath(testSha);
            File.Copy(exportedGlbPath, cachedPath, overwrite: true);

            var desc = new SanitizedCloudAssetDescriptor
            {
                assetSha256Id = testSha,
                displayName = "SelfTest_Custom_Sculpture",
                glbUrl = $"file://{cachedPath}",
                fileSizeBytes = sizeBytes,
                estimatedRuntimeMemoryBytes = sizeBytes * 2L,
                timestampUtc = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            if (CompanionPairingAndQuotaClient.Instance.TryApproveSanitizedAsset(desc, out string err))
            {
                Debug.Log("<color=#00FF88>[Phase 7 Stress Test] Triggered async TimeBudgetPerFrameDeferAgent load! Check Profiler for Zero Frame Spikes.</color>");
            }
            else
            {
                Debug.LogError($"[Phase 7 Stress Test] Quota rejected test asset: {err}");
            }
        }

        private static void EnsureLinkXmlPreservationFile()
        {
            if (File.Exists(LinkXmlPath)) return;

            const string xmlContent =
@"<linker>
  <assembly fullname=""glTFast"" preserve=""all"" />
  <assembly fullname=""glTFast.Export"" preserve=""all"" />
  <assembly fullname=""Ktx"" preserve=""all"" />
  <assembly fullname=""Draco"" preserve=""all"" />
  <assembly fullname=""MeshoptDecompress"" preserve=""all"" />
</linker>";
            File.WriteAllText(LinkXmlPath, xmlContent);
            Debug.Log("[Phase 7 Wizard] Created Assets/link.xml to prevent IL2CPP stripping of KTX2/Draco/Meshopt decoders.");
        }
    }
}
#endif
