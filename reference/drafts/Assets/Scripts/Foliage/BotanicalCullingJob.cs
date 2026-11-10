using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace IslandBuilder.Foliage
{
    public struct BotanicalInstanceData
    {
        public float4x4 WorldMatrix;
        public float3 WorldPosition;
        public float UniformScale;
        public float4 ColorAndCultivar; // rgb = tint, w = LUT row V
        public int MeshArchetypeId;     // 0..110
        public byte IsActive;           // 1 = active, 0 = soft-deleted by Undo stack
    }

    public struct ArchetypeLodThresholds
    {
        public float BoundingRadius;
        public float Lod0DistSq;
        public float Lod1DistSq;
        public float Lod2DistSq;
        public float MaxCullDistSq;
        public byte IsNaturalGrass;
    }

    public struct VisibleInstanceOutput
    {
        public int InstanceIndex;
        public int SortKey; // (MeshArchetypeId << 2) | LodLevel (0..3)
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    public struct BotanicalFrustumAndLodJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BotanicalInstanceData> Instances;
        [ReadOnly] public NativeArray<ArchetypeLodThresholds> ArchetypeThresholds;
        [ReadOnly] public NativeArray<float4> FrustumPlanes; // 6 planes: xyz = normal, w = distance
        [ReadOnly] public float3 CameraWorldPos;
        [ReadOnly] public float DistanceScaleMultiplierSq;   // Accounts for 1:25 Tabletop Scale vs 1:1 Scale
        [ReadOnly] public bool HideNaturalGrassForAstroturf;

        [WriteOnly] public NativeList<VisibleInstanceOutput>.ParallelWriter VisibleOutputs;

        public void Execute(int index)
        {
            BotanicalInstanceData inst = Instances[index];
            if (inst.IsActive == 0) return;

            ArchetypeLodThresholds thresh = ArchetypeThresholds[inst.MeshArchetypeId];
            if (HideNaturalGrassForAstroturf && thresh.IsNaturalGrass != 0)
            {
                return;
            }

            // 1. Evaluate distance squared adjusted for Dual-Scale Rig scale
            float rawDistSq = math.distancesq(inst.WorldPosition, CameraWorldPos);
            float effectiveDistSq = rawDistSq / math.max(0.0001f, DistanceScaleMultiplierSq);

            if (effectiveDistSq > thresh.MaxCullDistSq)
            {
                return;
            }

            // 2. Fast 6-plane Sphere-Frustum test
            float worldRadius = thresh.BoundingRadius * inst.UniformScale;
            for (int p = 0; p < 6; p++)
            {
                float4 plane = FrustumPlanes[p];
                float signedDist = math.dot(plane.xyz, inst.WorldPosition) + plane.w;
                if (signedDist < -worldRadius)
                {
                    return;
                }
            }

            // 3. Select 4-Tier LOD (0 = LOD0, 1 = LOD1, 2 = LOD2, 3 = LOD3 Billboard)
            int lodLevel = 0;
            if (effectiveDistSq > thresh.Lod2DistSq) lodLevel = 3;
            else if (effectiveDistSq > thresh.Lod1DistSq) lodLevel = 2;
            else if (effectiveDistSq > thresh.Lod0DistSq) lodLevel = 1;

            VisibleOutputs.AddNoResize(new VisibleInstanceOutput
            {
                InstanceIndex = index,
                SortKey = (inst.MeshArchetypeId << 2) | lodLevel
            });
        }
    }
}
