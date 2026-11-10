using System.Collections.Generic;
using IslandBuilder.UX;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandBuilder.Architecture
{
    public sealed class WeldedAssemblyMarker : MonoBehaviour
    {
        public Transform HiddenSourcesContainer;
        public Mesh CombinedMesh;

        private void OnDestroy()
        {
            if (CombinedMesh != null)
            {
                Destroy(CombinedMesh);
            }
        }
    }

    public sealed class RuntimeMeshWelder : MonoBehaviour
    {
        public static RuntimeMeshWelder Instance { get; private set; }

        [Header("Welding Settings")]
        [SerializeField] private Material sharedArchitecturalMaterial;
        [SerializeField] private float autoClusterRadiusMeters = 12.0f;
        [SerializeField] private int maxVerticesPerWeldedMesh = 64000;

        private readonly List<CombineInstance> _combineInstancesScratch = new List<CombineInstance>(256);
        private readonly List<GameObject> _weldedSourceObjectsScratch = new List<GameObject>(256);

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
        /// Welds a list of placed modular building / prop GameObjects sharing the Architectural Atlas
        /// into a single 1-Draw-Call static mesh while preserving the originals for non-destructive Unwelding.
        /// </summary>
        public GameObject WeldModulesIntoSingleDrawCall(IReadOnlyList<GameObject> candidateObjects, string assemblyName = "Welded_Island_Structure")
        {
            if (candidateObjects == null || candidateObjects.Count < 2)
            {
                return null;
            }

            _combineInstancesScratch.Clear();
            _weldedSourceObjectsScratch.Clear();

            Vector3 centroid = Vector3.zero;
            int validCount = 0;
            int runningVertexCount = 0;
            Material targetMat = sharedArchitecturalMaterial;

            for (int i = 0; i < candidateObjects.Count; i++)
            {
                GameObject obj = candidateObjects[i];
                if (obj == null || !obj.activeInHierarchy) continue;

                // Skip objects that are already welded assemblies or live media screens
                if (obj.GetComponent<WeldedAssemblyMarker>() != null) continue;

                var snapper = obj.GetComponent<MagneticSocketSnapper>();
                if (snapper != null && !snapper.CanBeWelded) continue;

                MeshFilter mf = obj.GetComponentInChildren<MeshFilter>();
                MeshRenderer mr = obj.GetComponentInChildren<MeshRenderer>();
                if (mf == null || mf.sharedMesh == null || mr == null) continue;

                if (targetMat == null) targetMat = mr.sharedMaterial;
                if (mr.sharedMaterial != targetMat) continue;

                int meshVerts = mf.sharedMesh.vertexCount;
                if (runningVertexCount + meshVerts > maxVerticesPerWeldedMesh)
                {
                    break;
                }

                runningVertexCount += meshVerts;
                centroid += obj.transform.position;
                validCount++;
                _weldedSourceObjectsScratch.Add(obj);
            }

            if (validCount < 2)
            {
                return null;
            }

            centroid /= validCount;

            // Create the new 1-Draw-Call Welded Assembly GameObject at the cluster centroid
            GameObject weldedGo = new GameObject(assemblyName);
            weldedGo.transform.position = centroid;
            weldedGo.transform.rotation = Quaternion.identity;

            Matrix4x4 worldToLocal = weldedGo.transform.worldToLocalMatrix;

            for (int i = 0; i < _weldedSourceObjectsScratch.Count; i++)
            {
                MeshFilter mf = _weldedSourceObjectsScratch[i].GetComponentInChildren<MeshFilter>();
                _combineInstancesScratch.Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    subMeshIndex = 0,
                    transform = worldToLocal * mf.transform.localToWorldMatrix
                });
            }

            Mesh combinedMesh = new Mesh
            {
                name = $"{assemblyName}_CombinedMesh",
                indexFormat = runningVertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            combinedMesh.CombineMeshes(_combineInstancesScratch.ToArray(), mergeSubMeshes: true, useMatrices: true, hasLightmapData: false);
            combinedMesh.RecalculateBounds();

            MeshFilter weldedMf = weldedGo.AddComponent<MeshFilter>();
            weldedMf.sharedMesh = combinedMesh;

            MeshRenderer weldedMr = weldedGo.AddComponent<MeshRenderer>();
            weldedMr.sharedMaterial = targetMat;
            weldedMr.shadowCastingMode = ShadowCastingMode.On;
            weldedMr.receiveShadows = true;

            // Pre-bake combined MeshCollider so the player and pets can walk across welded bridges/stairs
            Physics.BakeMesh(combinedMesh.GetInstanceID(), false);
            MeshCollider weldedCol = weldedGo.AddComponent<MeshCollider>();
            weldedCol.sharedMesh = combinedMesh;

            // Store deactivated source modules inside a hidden child container so Unwelding is 100% lossless
            GameObject sourcesContainer = new GameObject("_SourceModules_Container");
            sourcesContainer.transform.SetParent(weldedGo.transform, false);
            sourcesContainer.SetActive(false);

            for (int i = 0; i < _weldedSourceObjectsScratch.Count; i++)
            {
                GameObject src = _weldedSourceObjectsScratch[i];
                src.transform.SetParent(sourcesContainer.transform, true);
                src.SetActive(false);
            }

            WeldedAssemblyMarker marker = weldedGo.AddComponent<WeldedAssemblyMarker>();
            marker.HiddenSourcesContainer = sourcesContainer.transform;
            marker.CombinedMesh = combinedMesh;

            // Record in Phase 3 Undo/Redo Stack
            if (UndoRedoCommandStack.Instance != null)
            {
                UndoRedoCommandStack.Instance.RecordCommand(new IslandCommandRecord
                {
                    ActionType = CommandActionType.SpawnObject,
                    BatchGroupId = UndoRedoCommandStack.Instance.BeginCommandBatch(),
                    TargetObject = weldedGo,
                    AfterState = new TransformSnapshot
                    {
                        Position = weldedGo.transform.position,
                        Rotation = weldedGo.transform.rotation,
                        Scale = Vector3.one
                    }
                });
            }

            Debug.Log($"[RuntimeMeshWelder] Welded {_weldedSourceObjectsScratch.Count} modules ({runningVertexCount} verts) into 1 Draw Call: '{assemblyName}'.");
            return weldedGo;
        }

        /// <summary>
        /// Restores a welded 1-draw-call structure back into its individual editable modular pieces.
        /// </summary>
        public bool UnweldAssembly(GameObject weldedAssemblyObject, Transform restoreParent = null)
        {
            if (weldedAssemblyObject == null) return false;
            WeldedAssemblyMarker marker = weldedAssemblyObject.GetComponent<WeldedAssemblyMarker>();
            if (marker == null || marker.HiddenSourcesContainer == null) return false;

            int childCount = marker.HiddenSourcesContainer.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = marker.HiddenSourcesContainer.GetChild(i);
                child.SetParent(restoreParent, true);
                child.gameObject.SetActive(true);
            }

            Destroy(weldedAssemblyObject);
            return true;
        }

        /// <summary>
        /// Convenience method: Finds all unwelded modular building pieces within autoClusterRadiusMeters of a point
        /// and welds them into a single draw call.
        /// </summary>
        public GameObject WeldNearbyModulesAtPoint(Vector3 worldCenter)
        {
            MagneticSocketSnapper[] allModules = FindObjectsByType<MagneticSocketSnapper>(FindObjectsSortMode.None);
            _weldedSourceObjectsScratch.Clear();

            float radSq = autoClusterRadiusMeters * autoClusterRadiusMeters;
            for (int i = 0; i < allModules.Length; i++)
            {
                if (!allModules[i].CanBeWelded || !allModules[i].gameObject.activeInHierarchy) continue;
                if ((allModules[i].transform.position - worldCenter).sqrMagnitude <= radSq)
                {
                    _weldedSourceObjectsScratch.Add(allModules[i].gameObject);
                }
            }

            return WeldModulesIntoSingleDrawCall(_weldedSourceObjectsScratch, "Welded_Villa_Cluster");
        }
    }
}
