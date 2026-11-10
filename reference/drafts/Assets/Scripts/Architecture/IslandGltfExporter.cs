using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast.Export;
using GLTFast.Logging;
using IslandBuilder.Core;
using UnityEngine;

namespace IslandBuilder.Architecture
{
    public sealed class IslandGltfExporter : MonoBehaviour
    {
        public static IslandGltfExporter Instance { get; private set; }

        [Header("Export Configuration")]
        [SerializeField] private string exportSubfolder = "Exports";
        [SerializeField] private bool deterministicExport = true;

        private bool _isExportInProgress;

        public bool IsExportInProgress => _isExportInProgress;
        public event Action<bool, string> OnExportCompleted;

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
        /// Exports a custom kitbashed assembly (or an array of GameObjects) to a binary .glb file at runtime.
        /// </summary>
        public async Task<string> ExportHierarchyToGlbAsync(GameObject[] rootObjectsToExport, string fileBaseName)
        {
            if (_isExportInProgress || rootObjectsToExport == null || rootObjectsToExport.Length == 0)
            {
                return string.Empty;
            }

            _isExportInProgress = true;

            // Engage Application SpaceWarp during async file serialization to protect 90Hz frame pacing
            if (DynamicSpaceWarpController.Instance != null)
            {
                DynamicSpaceWarpController.Instance.AcquireSpaceWarpLock();
            }

            string outputFolder = Path.Combine(Application.persistentDataPath, exportSubfolder);
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string sanitizedName = SanitizeFileName(fileBaseName);
            string fullGlbPath = Path.Combine(outputFolder, $"{sanitizedName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.glb");

            bool success = false;
            var logger = new ConsoleLogger();

            try
            {
                var exportSettings = new ExportSettings
                {
                    Format = GltfFormat.Binary,
                    FileConflictResolution = FileConflictResolution.Overwrite,
                    Deterministic = deterministicExport,
                    // Exclude disabled source containers inside Welded Assemblies so only the optimized mesh exports
                    ComponentMask = ~(ComponentType.Camera | ComponentType.Animation)
                };

                var gameObjectExportSettings = new GameObjectExportSettings
                {
                    OnlyActiveInHierarchy = true,
                    DisabledComponents = false
                };

                var exporter = new GameObjectExport(exportSettings, gameObjectExportSettings, logger: logger);
                exporter.AddScene(rootObjectsToExport, sanitizedName);

                using (var fileStream = new FileStream(fullGlbPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                {
                    success = await exporter.SaveToStreamAndDispose(fileStream);
                }

                if (success)
                {
                    Debug.Log($"<color=#00FF88>[IslandGltfExporter] Successfully exported .glb ({new FileInfo(fullGlbPath).Length / 1024} KB) -> {fullGlbPath}</color>");
                }
                else
                {
                    Debug.LogError($"[IslandGltfExporter] glTFast export reported failure for '{fullGlbPath}'. Check ConsoleLogger output above.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IslandGltfExporter] Exception during .glb runtime export: {ex.Message}");
                success = false;
            }
            finally
            {
                if (DynamicSpaceWarpController.Instance != null)
                {
                    DynamicSpaceWarpController.Instance.ReleaseSpaceWarpLock();
                }
                _isExportInProgress = false;
            }

            OnExportCompleted?.Invoke(success, success ? fullGlbPath : string.Empty);
            return success ? fullGlbPath : string.Empty;
        }

        /// <summary>
        /// Exports a complete 3D snapshot of the player's 1-Acre Island (64 terrain chunks + welded structures + placed props).
        /// </summary>
        public async Task<string> ExportFullOneAcreIslandSnapshotAsync()
        {
            GameObject terrainRoot = GameObject.Find("Island_64_Chunks_Root");
            GameObject placedRoot = GameObject.Find("Placed_Island_Items_Root");

            GameObject[] roots = terrainRoot != null && placedRoot != null
                ? new[] { terrainRoot, placedRoot }
                : (terrainRoot != null ? new[] { terrainRoot } : Array.Empty<GameObject>());

            return await ExportHierarchyToGlbAsync(roots, "OneAcre_Island_Snapshot");
        }

        private static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Island_Creation";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(c, '_');
            }
            return raw;
        }
    }
}
