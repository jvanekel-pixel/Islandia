using System;
using System.Collections.Generic;
using IslandBuilder.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandBuilder.TerrainSystem
{
    public sealed class IslandTerrainManager : MonoBehaviour
    {
        public static IslandTerrainManager Instance { get; private set; }

        public const int ChunksPerSide = 8;
        public const int TotalChunks = ChunksPerSide * ChunksPerSide; // 64
        public const float ChunkSizeMeters = 8.0f;
        public const float IslandSizeMeters = ChunksPerSide * ChunkSizeMeters; // 64.0m (1 Acre)
        public const float VertexSpacing = 0.25f; // 25cm
        public const int GlobalResolution = ChunksPerSide * IslandTerrainChunk.QuadsPerSide + 1; // 257

        [Header("Materials & Shaders")]
        [SerializeField] private Material terrainUberMaterial;
        [SerializeField] private Material vulkanWaterMaterial;

        [Header("Shoreline & Height Configuration")]
        [SerializeField] private float minElevationMeters = -3.5f;
        [SerializeField] private float maxElevationMeters = 18.0f;
        [SerializeField] private float waterLevelMeters = -0.35f;
        [SerializeField] private float oceanPlaneSizeMeters = 512.0f;
        [SerializeField] private int oceanGridSubdivisions = 64;

        [Header("Grass vs. Astroturf Mode")]
        [SerializeField] private bool useAstroturfMode;

        private static readonly int AstroturfModeShaderId = Shader.PropertyToID("_UseAstroturfMode");

        private IslandTerrainChunk[] _chunks;
        private NativeArray<float> _heightsBufferA;
        private NativeArray<float> _heightsBufferB;
        private NativeArray<Color32> _globalColors;

        private readonly List<int> _dirtyChunkIndices = new List<int>(16);
        private readonly List<JobHandle> _activeChunkJobHandles = new List<JobHandle>(16);
        private readonly List<int> _dirtyColliderChunkIndices = new List<int>(64);

        /// <summary>
        /// Fired whenever chunks finish rebuilding and cooking colliders (used by Phase 11 Chunked NavMesh).
        /// </summary>
        public event Action<IReadOnlyList<int2>> OnChunksColliderUpdated;
        private readonly List<int2> _updatedChunkCoordsScratch = new List<int2>(64);

        public bool UseAstroturfMode
        {
            get => useAstroturfMode;
            set
            {
                useAstroturfMode = value;
                ApplyAstroturfShaderKeyword();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            DisablePhase1PlaceholderSpawnerIfPresent();
            AllocateGlobalNativeBuffers();
            InitializeInitialOneAcreShorelineProfile();
            Spawn64TerrainChunks();
            SpawnSurroundingOceanMesh();
            ApplyAstroturfShaderKeyword();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && terrainUberMaterial != null)
            {
                ApplyAstroturfShaderKeyword();
            }
        }

        private void OnDestroy()
        {
            if (_heightsBufferA.IsCreated) _heightsBufferA.Dispose();
            if (_heightsBufferB.IsCreated) _heightsBufferB.Dispose();
            if (_globalColors.IsCreated) _globalColors.Dispose();
        }

        private static void DisablePhase1PlaceholderSpawnerIfPresent()
        {
            var phase1Spawner = FindFirstObjectByType<Phase1BaselineIslandSpawner>();
            if (phase1Spawner != null)
            {
                phase1Spawner.gameObject.SetActive(false);
            }
        }

        private void AllocateGlobalNativeBuffers()
        {
            int totalGlobalVerts = GlobalResolution * GlobalResolution; // 66,049
            _heightsBufferA = new NativeArray<float>(totalGlobalVerts, Allocator.Persistent);
            _heightsBufferB = new NativeArray<float>(totalGlobalVerts, Allocator.Persistent);
            _globalColors = new NativeArray<Color32>(totalGlobalVerts, Allocator.Persistent);
        }

        /// <summary>
        /// Initializes a flat 1-acre tan island at Y = 0.0m with a smooth rounded beach drop-off
        /// into the surrounding water at the outer 3.5m perimeter.
        /// </summary>
        private void InitializeInitialOneAcreShorelineProfile()
        {
            float halfIsland = IslandSizeMeters * 0.5f; // 32m

            for (int gz = 0; gz < GlobalResolution; gz++)
            {
                float worldZ = -halfIsland + gz * VertexSpacing;
                for (int gx = 0; gx < GlobalResolution; gx++)
                {
                    float worldX = -halfIsland + gx * VertexSpacing;
                    int idx = gz * GlobalResolution + gx;

                    // Signed distance to a rounded 60m x 60m square island with 6m corner rounding
                    float2 p = new float2(math.abs(worldX), math.abs(worldZ)) - new float2(23.5f, 23.5f);
                    float roundedBoxDist = math.length(math.max(p, 0.0f)) + math.min(math.max(p.x, p.y), 0.0f) - 5.5f;

                    float height = 0.0f;
                    if (roundedBoxDist > 0.0f)
                    {
                        // Smooth beach slope down into the ocean bed (-1.2m)
                        float t = math.saturate(roundedBoxDist / 3.2f);
                        float smoothT = t * t * (3.0f - 2.0f * t);
                        height = math.lerp(0.0f, -1.25f, smoothT);
                    }

                    _heightsBufferA[idx] = height;
                    _heightsBufferB[idx] = height;

                    // Start 100% Flat Tan Ground / Sand (Red Channel = 255) per Blueprint Spec
                    _globalColors[idx] = new Color32(255, 0, 0, 0);
                }
            }
        }

        private void Spawn64TerrainChunks()
        {
            _chunks = new IslandTerrainChunk[TotalChunks];

            // Precompute shared UV and Triangle index arrays once for all 64 chunks
            Vector2[] sharedUvs = new Vector2[IslandTerrainChunk.TotalVertices];
            for (int z = 0; z < IslandTerrainChunk.VertsPerSide; z++)
            {
                for (int x = 0; x < IslandTerrainChunk.VertsPerSide; x++)
                {
                    sharedUvs[z * IslandTerrainChunk.VertsPerSide + x] = new Vector2(
                        (float)x / IslandTerrainChunk.QuadsPerSide,
                        (float)z / IslandTerrainChunk.QuadsPerSide);
                }
            }

            int[] sharedTris = new int[IslandTerrainChunk.QuadsPerSide * IslandTerrainChunk.QuadsPerSide * 6];
            int t = 0;
            for (int z = 0; z < IslandTerrainChunk.QuadsPerSide; z++)
            {
                for (int x = 0; x < IslandTerrainChunk.QuadsPerSide; x++)
                {
                    int bl = z * IslandTerrainChunk.VertsPerSide + x;
                    int br = bl + 1;
                    int tl = bl + IslandTerrainChunk.VertsPerSide;
                    int tr = tl + 1;

                    sharedTris[t++] = bl;
                    sharedTris[t++] = tl;
                    sharedTris[t++] = tr;

                    sharedTris[t++] = bl;
                    sharedTris[t++] = tr;
                    sharedTris[t++] = br;
                }
            }

            float halfIsland = IslandSizeMeters * 0.5f;
            GameObject chunksRoot = new GameObject("Island_64_Chunks_Root");
            chunksRoot.transform.SetParent(transform, false);

            for (int cz = 0; cz < ChunksPerSide; cz++)
            {
                for (int cx = 0; cx < ChunksPerSide; cx++)
                {
                    int chunkIdx = cz * ChunksPerSide + cx;
                    GameObject chunkGo = new GameObject($"Chunk_{cx}_{cz}");
                    chunkGo.layer = gameObject.layer;
                    chunkGo.transform.SetParent(chunksRoot.transform, false);
                    chunkGo.transform.localPosition = new Vector3(
                        -halfIsland + cx * ChunkSizeMeters,
                        0.0f,
                        -halfIsland + cz * ChunkSizeMeters);

                    IslandTerrainChunk chunk = chunkGo.AddComponent<IslandTerrainChunk>();
                    chunk.Initialize(new int2(cx, cz), VertexSpacing, terrainUberMaterial, sharedUvs, sharedTris);
                    _chunks[chunkIdx] = chunk;
                }
            }

            // Perform initial parallel mesh build across all 64 chunks
            _activeChunkJobHandles.Clear();
            for (int i = 0; i < TotalChunks; i++)
            {
                _activeChunkJobHandles.Add(_chunks[i].ScheduleMeshRebuild(
                    _heightsBufferA, _globalColors, GlobalResolution, VertexSpacing));
                _dirtyColliderChunkIndices.Add(i);
            }

            CompleteActiveChunkMeshJobsAndUpload();
            FlushDeferredColliderBaking();
        }

        private void SpawnSurroundingOceanMesh()
        {
            GameObject oceanGo = new GameObject("Island_Shoreline_Ocean");
            oceanGo.transform.SetParent(transform, false);
            oceanGo.transform.localPosition = new Vector3(0f, waterLevelMeters, 0f);

            MeshFilter mf = oceanGo.AddComponent<MeshFilter>();
            MeshRenderer mr = oceanGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = vulkanWaterMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            int vertsPerSide = oceanGridSubdivisions + 1;
            Vector3[] verts = new Vector3[vertsPerSide * vertsPerSide];
            Vector3[] norms = new Vector3[verts.Length];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[oceanGridSubdivisions * oceanGridSubdivisions * 6];

            float half = oceanPlaneSizeMeters * 0.5f;
            float step = oceanPlaneSizeMeters / oceanGridSubdivisions;

            int v = 0;
            for (int z = 0; z <= oceanGridSubdivisions; z++)
            {
                for (int x = 0; x <= oceanGridSubdivisions; x++)
                {
                    verts[v] = new Vector3(-half + x * step, 0f, -half + z * step);
                    norms[v] = Vector3.up;
                    uvs[v] = new Vector2((float)x / oceanGridSubdivisions, (float)z / oceanGridSubdivisions);
                    v++;
                }
            }

            int ti = 0;
            for (int z = 0; z < oceanGridSubdivisions; z++)
            {
                for (int x = 0; x < oceanGridSubdivisions; x++)
                {
                    int bl = z * vertsPerSide + x;
                    int br = bl + 1;
                    int tl = bl + vertsPerSide;
                    int tr = tl + 1;

                    tris[ti++] = bl;
                    tris[ti++] = tl;
                    tris[ti++] = tr;
                    tris[ti++] = bl;
                    tris[ti++] = tr;
                    tris[ti++] = br;
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Ocean_Subdivided_Mesh",
                indexFormat = IndexFormat.UInt16
            };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }

        /// <summary>
        /// Executes real-time Burst sculpting & vertex painting on only the 1 to 4 chunks intersected by the brush.
        /// Call every frame while the player holds the VR trigger.
        /// </summary>
        public void ApplyBrushStroke(
            Vector3 worldHitPoint,
            float brushRadius,
            float brushStrength,
            TerraformingBrushMode mode,
            TerrainSurfaceLayer paintLayer,
            float flattenTargetHeight,
            bool alsoPaintWhileSculpting)
        {
            float halfIsland = IslandSizeMeters * 0.5f;
            float2 minWorldXZ = new float2(-halfIsland, -halfIsland);
            float2 hitXZ = new float2(worldHitPoint.x, worldHitPoint.z);

            // Convert world brush bounds + 1 vertex normal padding into global [0..256] index window
            float padRadius = brushRadius + VertexSpacing * 2.0f;
            int minGx = math.clamp((int)math.floor((hitXZ.x - padRadius - minWorldXZ.x) / VertexSpacing), 0, GlobalResolution - 1);
            int maxGx = math.clamp((int)math.ceil((hitXZ.x + padRadius - minWorldXZ.x) / VertexSpacing), 0, GlobalResolution - 1);
            int minGz = math.clamp((int)math.floor((hitXZ.y - padRadius - minWorldXZ.y) / VertexSpacing), 0, GlobalResolution - 1);
            int maxGz = math.clamp((int)math.ceil((hitXZ.y + padRadius - minWorldXZ.y) / VertexSpacing), 0, GlobalResolution - 1);

            int windowWidth = (maxGx - minGx) + 1;
            int windowHeight = (maxGz - minGz) + 1;
            int windowTotalVerts = windowWidth * windowHeight;
            if (windowTotalVerts <= 0) return;

            // Schedule Burst Sculpt & Paint Job on the bounded vertex window
            var sculptJob = new TerrainSculptAndPaintJob
            {
                GlobalResolution = GlobalResolution,
                VertexSpacing = VertexSpacing,
                IslandMinWorldXZ = minWorldXZ,
                float2HeightLimits = new float2(minElevationMeters, maxElevationMeters),
                BrushWorldXZ = hitXZ,
                BrushRadius = brushRadius,
                BrushStrength = brushStrength,
                DeltaTime = Time.deltaTime,
                FlattenTargetHeight = flattenTargetHeight,
                BrushMode = mode,
                PaintLayer = paintLayer,
                AlsoPaintWhileSculpting = alsoPaintWhileSculpting,
                ReadHeights = _heightsBufferA,
                WriteHeights = _heightsBufferB,
                VertexColors = _globalColors,
                StartX = minGx,
                StartZ = minGz,
                WindowWidth = windowWidth
            };

            JobHandle sculptHandle = sculptJob.Schedule(windowTotalVerts, 64);
            sculptHandle.Complete();

            // Copy modified window back to Buffer A so both buffers remain synchronized for Smooth kernel reads
            for (int gz = minGz; gz <= maxGz; gz++)
            {
                int rowOffset = gz * GlobalResolution + minGx;
                NativeArray<float>.Copy(_heightsBufferB, rowOffset, _heightsBufferA, rowOffset, windowWidth);
            }

            // Identify the 1 to 4 chunks intersected by the modified vertex window
            int minChunkX = math.clamp((minGx - 1) / IslandTerrainChunk.QuadsPerSide, 0, ChunksPerSide - 1);
            int maxChunkX = math.clamp((maxGx + 1) / IslandTerrainChunk.QuadsPerSide, 0, ChunksPerSide - 1);
            int minChunkZ = math.clamp((minGz - 1) / IslandTerrainChunk.QuadsPerSide, 0, ChunksPerSide - 1);
            int maxChunkZ = math.clamp((maxGz + 1) / IslandTerrainChunk.QuadsPerSide, 0, ChunksPerSide - 1);

            _dirtyChunkIndices.Clear();
            _activeChunkJobHandles.Clear();

            for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
            {
                for (int cx = minChunkX; cx <= maxChunkX; cx++)
                {
                    int chunkIdx = cz * ChunksPerSide + cx;
                    _dirtyChunkIndices.Add(chunkIdx);

                    if (!_dirtyColliderChunkIndices.Contains(chunkIdx))
                    {
                        _dirtyColliderChunkIndices.Add(chunkIdx);
                    }

                    JobHandle rebuildHandle = _chunks[chunkIdx].ScheduleMeshRebuild(
                        _heightsBufferA, _globalColors, GlobalResolution, VertexSpacing);
                    _activeChunkJobHandles.Add(rebuildHandle);
                }
            }

            CompleteActiveChunkMeshJobsAndUpload();
        }

        /// <summary>
        /// Automatically levels a circular foundation pad at a target world position (used when placing gazebos/cottages on hills).
        /// </summary>
        public void LevelCircularFoundationPad(Vector3 worldCenter, float padRadius, float targetElevation)
        {
            ApplyBrushStroke(
                worldCenter,
                padRadius,
                brushStrength: 40.0f,
                TerraformingBrushMode.Flatten,
                TerrainSurfaceLayer.GardenPath,
                targetElevation,
                alsoPaintWhileSculpting: true);
            FlushDeferredColliderBaking();
        }

        /// <summary>
        /// Samples exact terrain height at any world (X, Z) coordinate in O(1) time without Physics.Raycast overhead.
        /// </summary>
        public float SampleHeightAtWorldPosition(Vector3 worldPos)
        {
            float halfIsland = IslandSizeMeters * 0.5f;
            float u = (worldPos.x + halfIsland) / VertexSpacing;
            float v = (worldPos.z + halfIsland) / VertexSpacing;

            int x0 = math.clamp((int)math.floor(u), 0, GlobalResolution - 1);
            int x1 = math.clamp(x0 + 1, 0, GlobalResolution - 1);
            int z0 = math.clamp((int)math.floor(v), 0, GlobalResolution - 1);
            int z1 = math.clamp(z0 + 1, 0, GlobalResolution - 1);

            float tx = math.saturate(u - x0);
            float tz = math.saturate(v - z0);

            float h00 = _heightsBufferA[z0 * GlobalResolution + x0];
            float h10 = _heightsBufferA[z0 * GlobalResolution + x1];
            float h01 = _heightsBufferA[z1 * GlobalResolution + x0];
            float h11 = _heightsBufferA[z1 * GlobalResolution + x1];

            return math.lerp(math.lerp(h00, h10, tx), math.lerp(h01, h11, tx), tz);
        }

        /// <summary>
        /// Call when the player releases the VR sculpting trigger. Bakes MeshColliders in parallel
        /// across worker threads using Physics.BakeMesh so the main thread never stutters.
        /// </summary>
        public void FlushDeferredColliderBaking()
        {
            int count = _dirtyColliderChunkIndices.Count;
            if (count == 0) return;

            NativeArray<int> meshIds = new NativeArray<int>(count, Allocator.TempJob);
            _updatedChunkCoordsScratch.Clear();

            for (int i = 0; i < count; i++)
            {
                IslandTerrainChunk chunk = _chunks[_dirtyColliderChunkIndices[i]];
                meshIds[i] = chunk.MeshInstanceId;
                _updatedChunkCoordsScratch.Add(chunk.ChunkCoord);
            }

            var bakeJob = new DeferredColliderBakeJob
            {
                MeshInstanceIds = meshIds
            };

            JobHandle handle = bakeJob.Schedule(count, 1);
            handle.Complete();
            meshIds.Dispose();

            for (int i = 0; i < count; i++)
            {
                _chunks[_dirtyColliderChunkIndices[i]].RefreshBakedCollider();
            }

            _dirtyColliderChunkIndices.Clear();
            OnChunksColliderUpdated?.Invoke(_updatedChunkCoordsScratch);
        }

        private void CompleteActiveChunkMeshJobsAndUpload()
        {
            for (int i = 0; i < _activeChunkJobHandles.Count; i++)
            {
                _activeChunkJobHandles[i].Complete();
            }

            if (_dirtyChunkIndices.Count > 0)
            {
                for (int i = 0; i < _dirtyChunkIndices.Count; i++)
                {
                    _chunks[_dirtyChunkIndices[i]].ApplyRebuiltMeshToGpu();
                }
            }
            else
            {
                for (int i = 0; i < TotalChunks; i++)
                {
                    _chunks[i].ApplyRebuiltMeshToGpu();
                }
            }
        }

        private void ApplyAstroturfShaderKeyword()
        {
            if (terrainUberMaterial == null) return;
            terrainUberMaterial.SetFloat(AstroturfModeShaderId, useAstroturfMode ? 1.0f : 0.0f);
            if (useAstroturfMode)
            {
                terrainUberMaterial.EnableKeyword("_ASTROTURF_MODE_ON");
            }
            else
            {
                terrainUberMaterial.DisableKeyword("_ASTROTURF_MODE_ON");
            }
        }
    }
}
