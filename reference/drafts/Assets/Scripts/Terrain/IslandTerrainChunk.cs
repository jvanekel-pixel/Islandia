using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandBuilder.TerrainSystem
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class IslandTerrainChunk : MonoBehaviour, IDisposable
    {
        public const int VertsPerSide = 33;
        public const int QuadsPerSide = 32;
        public const int TotalVertices = VertsPerSide * VertsPerSide; // 1,089

        public int2 ChunkCoord { get; private set; }
        public int GlobalStartX { get; private set; }
        public int GlobalStartZ { get; private set; }
        public bool IsColliderDirty { get; set; }
        public int MeshInstanceId => _mesh != null ? _mesh.GetInstanceID() : 0;

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;

        private NativeArray<float3> _vertices;
        private NativeArray<float3> _normals;
        private NativeArray<Color32> _colors;

        private static readonly MeshUpdateFlags FastUploadFlags =
            MeshUpdateFlags.DontValidateIndices |
            MeshUpdateFlags.DontNotifyMeshUsers |
            MeshUpdateFlags.DontResetBoneBounds |
            MeshUpdateFlags.DontRecalculateBounds;

        public void Initialize(
            int2 chunkCoord,
            float vertexSpacing,
            Material sharedTerrainMaterial,
            Vector2[] sharedUvs,
            int[] sharedTriangles)
        {
            ChunkCoord = chunkCoord;
            GlobalStartX = chunkCoord.x * QuadsPerSide;
            GlobalStartZ = chunkCoord.y * QuadsPerSide;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshCollider = GetComponent<MeshCollider>();

            // Terrain chunks receive shadows from the player/pet within 15m, but do not cast shadows
            // unless desired, saving shadow-caster passes across 64 chunks.
            _meshRenderer.sharedMaterial = sharedTerrainMaterial;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = true;

            _vertices = new NativeArray<float3>(TotalVertices, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _normals = new NativeArray<float3>(TotalVertices, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _colors = new NativeArray<Color32>(TotalVertices, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            _mesh = new Mesh
            {
                name = $"TerrainChunk_{chunkCoord.x}_{chunkCoord.y}",
                indexFormat = IndexFormat.UInt16
            };
            _mesh.MarkDynamic();

            // Initialize dummy positions so sharedTriangles validation passes on creation
            for (int i = 0; i < TotalVertices; i++)
            {
                int lx = i % VertsPerSide;
                int lz = i / VertsPerSide;
                _vertices[i] = new float3(lx * vertexSpacing, 0f, lz * vertexSpacing);
                _normals[i] = new float3(0f, 1f, 0f);
                _colors[i] = new Color32(255, 0, 0, 0);
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, sharedUvs);
            _mesh.SetTriangles(sharedTriangles, 0, false);

            // Fixed generous vertical bounds [-5m to +20m] so we never need CPU RecalculateBounds() per frame
            _mesh.bounds = new Bounds(new Vector3(4.0f, 7.5f, 4.0f), new Vector3(8.0f, 26.0f, 8.0f));

            _meshFilter.sharedMesh = _mesh;
            _meshCollider.sharedMesh = _mesh;
        }

        public JobHandle ScheduleMeshRebuild(
            NativeArray<float> globalHeights,
            NativeArray<Color32> globalColors,
            int globalResolution,
            float vertexSpacing,
            JobHandle dependency = default)
        {
            var job = new ChunkMeshRebuildJob
            {
                GlobalResolution = globalResolution,
                ChunkVertsPerSide = VertsPerSide,
                ChunkStartGlobalX = GlobalStartX,
                ChunkStartGlobalZ = GlobalStartZ,
                VertexSpacing = vertexSpacing,
                GlobalHeights = globalHeights,
                GlobalColors = globalColors,
                OutVertices = _vertices,
                OutNormals = _normals,
                OutColors = _colors
            };

            return job.Schedule(TotalVertices, 64, dependency);
        }

        public void ApplyRebuiltMeshToGpu()
        {
            _mesh.SetVertices(_vertices, 0, TotalVertices, FastUploadFlags);
            _mesh.SetNormals(_normals, 0, TotalVertices, FastUploadFlags);
            _mesh.SetColors(_colors, 0, TotalVertices, FastUploadFlags);
            IsColliderDirty = true;
        }

        public void RefreshBakedCollider()
        {
            if (!IsColliderDirty) return;
            // Assigning sharedMesh after Physics.BakeMesh on a worker thread links the pre-cooked PhysX mesh
            // without running synchronous cooking on the main thread.
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _mesh;
            IsColliderDirty = false;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_vertices.IsCreated) _vertices.Dispose();
            if (_normals.IsCreated) _normals.Dispose();
            if (_colors.IsCreated) _colors.Dispose();
        }
    }
}
