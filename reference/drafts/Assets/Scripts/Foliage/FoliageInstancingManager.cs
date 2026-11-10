using System;
using System.Collections.Generic;
using IslandBuilder.TerrainSystem;
using IslandBuilder.UX;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandBuilder.Foliage
{
    public sealed class FoliageInstancingManager : MonoBehaviour, IDisposable
    {
        public static FoliageInstancingManager Instance { get; private set; }

        // Vulkan Single-Pass Stereo UBO safe batch limit per DrawMeshInstanced call
        private const int MaxInstancesPerBatch = 511;
        private const int MaxTotalBotanicalInstances = 16384;
        private const int TotalSortBuckets = BotanicalCatalogDatabase.TotalMeshArchetypes * 4; // 111 * 4 LODs = 444

        [Header("Catalog & Camera References")]
        [SerializeField] private BotanicalCatalogDatabase catalogDatabase;
        [SerializeField] private Camera cullingCamera;

        [Header("Initial Island Meadow Seeding")]
        [SerializeField] private bool seedStarterBotanicalGardenOnAwake = true;
        [SerializeField] private int starterGrassTuftCount = 1800;

        private static readonly int InstanceColorAndCultivarShaderId = Shader.PropertyToID("_InstanceColorAndCultivar");

        private NativeArray<BotanicalInstanceData> _instances;
        private NativeArray<ArchetypeLodThresholds> _archetypeThresholds;
        private NativeArray<float4> _frustumPlanesNative;
        private NativeList<VisibleInstanceOutput> _visibleOutputs;

        private int _activeInstanceCount;
        private readonly Plane[] _unityPlanesScratch = new Plane[6];

        // Pre-allocated per-bucket batch lists to guarantee 0 B GC allocation during rendering
        private List<int>[] _bucketInstanceIndices;
        private Matrix4x4[] _batchMatricesScratch;
        private Vector4[] _batchCultivarColorsScratch;
        private MaterialPropertyBlock _sharedPropertyBlock;

        public int ActiveInstanceCount => _activeInstanceCount;
        public BotanicalCatalogDatabase CatalogDatabase => catalogDatabase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (cullingCamera == null) cullingCamera = Camera.main;

            AllocatePersistentBuffers();
            CacheArchetypeThresholdsFromCatalog();
        }

        private void Start()
        {
            // Subscribe to Phase 3 Toybox Scatter Brush & Phase 2 Terrain Sculpt Updates
            var toybox = FindFirstObjectByType<ToyboxInteractionController>();
            if (toybox != null)
            {
                toybox.OnBotanicalInstanceScattered += HandleToyboxScatteredInstance;
            }

            if (IslandTerrainManager.Instance != null)
            {
                IslandTerrainManager.Instance.OnChunksColliderUpdated += HandleTerrainChunksModified;
            }

            if (seedStarterBotanicalGardenOnAwake && catalogDatabase != null)
            {
                SeedDefaultNaturalGrassAndSampleFlora();
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_instances.IsCreated) _instances.Dispose();
            if (_archetypeThresholds.IsCreated) _archetypeThresholds.Dispose();
            if (_frustumPlanesNative.IsCreated) _frustumPlanesNative.Dispose();
            if (_visibleOutputs.IsCreated) _visibleOutputs.Dispose();
        }

        private void AllocatePersistentBuffers()
        {
            _instances = new NativeArray<BotanicalInstanceData>(MaxTotalBotanicalInstances, Allocator.Persistent);
            _archetypeThresholds = new NativeArray<ArchetypeLodThresholds>(BotanicalCatalogDatabase.TotalMeshArchetypes, Allocator.Persistent);
            _frustumPlanesNative = new NativeArray<float4>(6, Allocator.Persistent);
            _visibleOutputs = new NativeList<VisibleInstanceOutput>(MaxTotalBotanicalInstances, Allocator.Persistent);

            _bucketInstanceIndices = new List<int>[TotalSortBuckets];
            for (int i = 0; i < TotalSortBuckets; i++)
            {
                _bucketInstanceIndices[i] = new List<int>(128);
            }

            _batchMatricesScratch = new Matrix4x4[MaxInstancesPerBatch];
            _batchCultivarColorsScratch = new Vector4[MaxInstancesPerBatch];
            _sharedPropertyBlock = new MaterialPropertyBlock();
        }

        private void CacheArchetypeThresholdsFromCatalog()
        {
            if (catalogDatabase == null || catalogDatabase.MeshArchetypes == null) return;

            int count = Mathf.Min(catalogDatabase.MeshArchetypes.Length, BotanicalCatalogDatabase.TotalMeshArchetypes);
            for (int i = 0; i < count; i++)
            {
                BotanicalMeshEntry entry = catalogDatabase.MeshArchetypes[i];
                _archetypeThresholds[i] = new ArchetypeLodThresholds
                {
                    BoundingRadius = Mathf.Max(0.25f, entry.BoundingSphereRadius),
                    Lod0DistSq = entry.Lod0To1Distance * entry.Lod0To1Distance,
                    Lod1DistSq = entry.Lod1To2Distance * entry.Lod1To2Distance,
                    Lod2DistSq = entry.Lod2To3Distance * entry.Lod2To3Distance,
                    MaxCullDistSq = entry.MaxCullDistance * entry.MaxCullDistance,
                    IsNaturalGrass = (byte)(entry.Category == BotanicalCategory.NaturalGrass ? 1 : 0)
                };
            }
        }

        /// <summary>
        /// Registers any of the 398 pre-loaded botanical varieties (or Natural Grass ID 398) into the instanced renderer.
        /// Automatically enforces the 15%-20% bush/flower scale boost when requested.
        /// </summary>
        public int AddBotanicalInstance(
            int catalogVarietyId,
            Vector3 worldPosition,
            Quaternion worldRotation,
            float uniformScale,
            bool applyArchvizScaleBoost = false)
        {
            if (_activeInstanceCount >= MaxTotalBotanicalInstances || catalogDatabase == null)
            {
                return -1;
            }

            catalogDatabase.ResolveCatalogVariety(
                catalogVarietyId,
                out int meshArchetypeId,
                out float cultivarLutV,
                out Vector3 baseTint);

            BotanicalCategory cat = catalogDatabase.MeshArchetypes[meshArchetypeId].Category;
            if (applyArchvizScaleBoost && (cat == BotanicalCategory.BushOrShrub || cat == BotanicalCategory.FlowerCluster))
            {
                // Blueprint Mandate: Scale bushes & flowers up by 15%-20% to maximize volume with fewer instances
                uniformScale *= UnityEngine.Random.Range(1.15f, 1.20f);
            }

            int idx = _activeInstanceCount++;
            Matrix4x4 trs = Matrix4x4.TRS(worldPosition, worldRotation, Vector3.one * uniformScale);

            _instances[idx] = new BotanicalInstanceData
            {
                WorldMatrix = trs,
                WorldPosition = worldPosition,
                UniformScale = uniformScale,
                ColorAndCultivar = new float4(baseTint.x, baseTint.y, baseTint.z, cultivarLutV),
                MeshArchetypeId = meshArchetypeId,
                IsActive = 1
            };

            return idx;
        }

        public void SetInstanceActiveState(int instanceIndex, bool active)
        {
            if (instanceIndex < 0 || instanceIndex >= _activeInstanceCount) return;
            BotanicalInstanceData data = _instances[instanceIndex];
            data.IsActive = (byte)(active ? 1 : 0);
            _instances[instanceIndex] = data;
        }

        private void HandleToyboxScatteredInstance(int catalogVarietyId, Matrix4x4 trs, Vector4 customTint)
        {
            Vector3 pos = trs.GetColumn(3);
            Quaternion rot = trs.rotation;
            float scale = trs.lossyScale.x;
            AddBotanicalInstance(catalogVarietyId, pos, rot, scale, applyArchvizScaleBoost: false);
        }

        /// <summary>
        /// Whenever the player sculpts hills or ponds in Phase 2, automatically snap all botanical instances
        /// inside the modified 8m x 8m chunks to their new terrain elevation!
        /// </summary>
        private void HandleTerrainChunksModified(IReadOnlyList<int2> modifiedChunks)
        {
            var terrain = IslandTerrainManager.Instance;
            if (terrain == null || _activeInstanceCount == 0) return;

            for (int i = 0; i < _activeInstanceCount; i++)
            {
                BotanicalInstanceData inst = _instances[i];
                if (inst.IsActive == 0) continue;

                float newY = terrain.SampleHeightAtWorldPosition(inst.WorldPosition);
                // If terrain was excavated into a deep pond (< -0.30m), hide terrestrial grass/flowers automatically
                if (newY < -0.30f && inst.MeshArchetypeId >= 78)
                {
                    inst.IsActive = 0;
                }
                else
                {
                    inst.WorldPosition.y = newY;
                    inst.WorldMatrix.m13 = newY;
                }
                _instances[i] = inst;
            }
        }

        private void SeedDefaultNaturalGrassAndSampleFlora()
        {
            var terrain = IslandTerrainManager.Instance;
            UnityEngine.Random.InitState(42);

            // Scatter GPU-instanced Natural Grass blade tufts across the interior island
            for (int i = 0; i < starterGrassTuftCount; i++)
            {
                Vector2 disc = UnityEngine.Random.insideUnitCircle * 24.0f;
                Vector3 pos = new Vector3(disc.x, 0f, disc.y);
                if (terrain != null) pos.y = terrain.SampleHeightAtWorldPosition(pos);
                if (pos.y < -0.15f) continue;

                Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                float scale = UnityEngine.Random.Range(0.9f, 1.25f);
                AddBotanicalInstance(398, pos, rot, scale);
            }
        }

        private void LateUpdate()
        {
            if (_activeInstanceCount == 0 || catalogDatabase == null || catalogDatabase.SharedBotanicalMaterial == null)
            {
                return;
            }

            if (cullingCamera == null)
            {
                cullingCamera = Camera.main;
                if (cullingCamera == null) return;
            }

            ExecuteBurstFrustumAndLodCulling();
            SubmitInstancedBotanicalBatches();
        }

        private void ExecuteBurstFrustumAndLodCulling()
        {
            GeometryUtility.CalculateFrustumPlanes(cullingCamera, _unityPlanesScratch);
            for (int i = 0; i < 6; i++)
            {
                Vector3 n = _unityPlanesScratch[i].normal;
                _frustumPlanesNative[i] = new float4(n.x, n.y, n.z, _unityPlanesScratch[i].distance);
            }

            float rigScale = DualScaleWorldRigController.Instance != null
                ? DualScaleWorldRigController.Instance.CurrentRigScale
                : 1.0f;

            bool hideGrass = IslandTerrainManager.Instance != null && IslandTerrainManager.Instance.UseAstroturfMode;

            _visibleOutputs.Clear();

            var cullJob = new BotanicalFrustumAndLodJob
            {
                Instances = _instances,
                ArchetypeThresholds = _archetypeThresholds,
                FrustumPlanes = _frustumPlanesNative,
                CameraWorldPos = cullingCamera.transform.position,
                DistanceScaleMultiplierSq = rigScale * rigScale,
                HideNaturalGrassForAstroturf = hideGrass,
                VisibleOutputs = _visibleOutputs.AsParallelWriter()
            };

            JobHandle handle = cullJob.Schedule(_activeInstanceCount, 128);
            handle.Complete();
        }

        private void SubmitInstancedBotanicalBatches()
        {
            for (int b = 0; b < TotalSortBuckets; b++)
            {
                _bucketInstanceIndices[b].Clear();
            }

            int visibleCount = _visibleOutputs.Length;
            for (int i = 0; i < visibleCount; i++)
            {
                VisibleInstanceOutput vis = _visibleOutputs[i];
                if ((uint)vis.SortKey < (uint)TotalSortBuckets)
                {
                    _bucketInstanceIndices[vis.SortKey].Add(vis.InstanceIndex);
                }
            }

            Material sharedMat = catalogDatabase.SharedBotanicalMaterial;

            for (int bucketKey = 0; bucketKey < TotalSortBuckets; bucketKey++)
            {
                List<int> bucketList = _bucketInstanceIndices[bucketKey];
                int totalInBucket = bucketList.Count;
                if (totalInBucket == 0) continue;

                int archetypeId = bucketKey >> 2;
                int lodLevel = bucketKey & 3;

                BotanicalMeshEntry entry = catalogDatabase.MeshArchetypes[archetypeId];
                Mesh targetMesh = lodLevel switch
                {
                    0 => entry.Lod0Mesh,
                    1 => entry.Lod1Mesh != null ? entry.Lod1Mesh : entry.Lod0Mesh,
                    2 => entry.Lod2Mesh != null ? entry.Lod2Mesh : entry.Lod0Mesh,
                    _ => entry.Lod3BillboardMesh != null ? entry.Lod3BillboardMesh : entry.Lod2Mesh
                };

                if (targetMesh == null) continue;

                int offset = 0;
                while (offset < totalInBucket)
                {
                    int batchCount = Mathf.Min(MaxInstancesPerBatch, totalInBucket - offset);
                    for (int i = 0; i < batchCount; i++)
                    {
                        BotanicalInstanceData inst = _instances[bucketList[offset + i]];
                        _batchMatricesScratch[i] = inst.WorldMatrix;
                        _batchCultivarColorsScratch[i] = inst.ColorAndCultivar;
                    }

                    _sharedPropertyBlock.SetVectorArray(InstanceColorAndCultivarShaderId, _batchCultivarColorsScratch);

                    // ShadowCastingMode.Off enforced globally on all foliage per Blueprint Pillar 4
                    Graphics.DrawMeshInstanced(
                        targetMesh,
                        0,
                        sharedMat,
                        _batchMatricesScratch,
                        batchCount,
                        _sharedPropertyBlock,
                        ShadowCastingMode.Off,
                        receiveShadows: false,
                        layer: gameObject.layer,
                        camera: null,
                        LightProbeUsage.Off);

                    offset += batchCount;
                }
            }
        }
    }
}
