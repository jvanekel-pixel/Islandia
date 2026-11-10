using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace IslandBuilder.TerrainSystem
{
    public enum TerraformingBrushMode : byte
    {
        Raise = 0,
        Lower = 1,
        Flatten = 2,
        Smooth = 3,
        PaintSurface = 4
    }

    public enum TerrainSurfaceLayer : byte
    {
        TanSand = 0,       // Red Channel
        GrassOrTurf = 1,   // Green Channel
        CliffRock = 2,     // Blue Channel (also auto-applied on steep slopes)
        GardenPath = 3     // Alpha Channel
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    public struct TerrainSculptAndPaintJob : IJobParallelFor
    {
        // Global grid dimensions (257 x 257)
        [ReadOnly] public int GlobalResolution;
        [ReadOnly] public float VertexSpacing;
        [ReadOnly] public float2 IslandMinWorldXZ;
        [ReadOnly] public float2HeightLimits; // x = MinHeight (-3.5m), y = MaxHeight (+18.0m)

        // Brush parameters
        [ReadOnly] public float2 BrushWorldXZ;
        [ReadOnly] public float BrushRadius;
        [ReadOnly] public float BrushStrength;
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public float FlattenTargetHeight;
        [ReadOnly] public TerraformingBrushMode BrushMode;
        [ReadOnly] public TerrainSurfaceLayer PaintLayer;
        [ReadOnly] public bool AlsoPaintWhileSculpting;

        // Global buffers (Read/Write inside bounded brush window)
        [NativeDisableParallelForRestriction] public NativeArray<float> ReadHeights;
        [NativeDisableParallelForRestriction] public NativeArray<float> WriteHeights;
        [NativeDisableParallelForRestriction] public NativeArray<Color32> VertexColors;

        // Sub-window bounds of affected vertices
        [ReadOnly] public int StartX;
        [ReadOnly] public int StartZ;
        [ReadOnly] public int WindowWidth;

        public void Execute(int index)
        {
            int localX = index % WindowWidth;
            int localZ = index / WindowWidth;

            int gx = StartX + localX;
            int gz = StartZ + localZ;
            int globalIdx = gz * GlobalResolution + gx;

            float worldX = IslandMinWorldXZ.x + gx * VertexSpacing;
            float worldZ = IslandMinWorldXZ.y + gz * VertexSpacing;

            float dist = math.distance(new float2(worldX, worldZ), BrushWorldXZ);
            if (dist > BrushRadius)
            {
                return;
            }

            // Smooth cubic Hermite falloff: 1 at center, 0 at BrushRadius
            float normalizedDist = math.saturate(dist / math.max(0.001f, BrushRadius));
            float falloff = 1.0f - (normalizedDist * normalizedDist * (3.0f - 2.0f * normalizedDist));

            // Keep outer 1.5m island perimeter anchored near shoreline level so ocean edges stay clean
            float edgeDistX = math.min(gx, (GlobalResolution - 1) - gx) * VertexSpacing;
            float edgeDistZ = math.min(gz, (GlobalResolution - 1) - gz) * VertexSpacing;
            float islandBorderMask = math.saturate((math.min(edgeDistX, edgeDistZ) - 0.5f) / 2.0f);

            float currentH = ReadHeights[globalIdx];
            float newH = currentH;

            switch (BrushMode)
            {
                case TerraformingBrushMode.Raise:
                    newH += BrushStrength * falloff * DeltaTime * islandBorderMask;
                    break;

                case TerraformingBrushMode.Lower:
                    newH -= BrushStrength * falloff * DeltaTime * islandBorderMask;
                    break;

                case TerraformingBrushMode.Flatten:
                    float targetH = math.lerp(currentH, FlattenTargetHeight, islandBorderMask);
                    newH = math.lerp(currentH, targetH, math.saturate(BrushStrength * falloff * DeltaTime));
                    break;

                case TerraformingBrushMode.Smooth:
                    float sum = 0f;
                    int count = 0;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int nz = math.clamp(gz + dz, 0, GlobalResolution - 1);
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = math.clamp(gx + dx, 0, GlobalResolution - 1);
                            sum += ReadHeights[nz * GlobalResolution + nx];
                            count++;
                        }
                    }
                    float avgH = sum / count;
                    newH = math.lerp(currentH, avgH, math.saturate(BrushStrength * falloff * DeltaTime * islandBorderMask));
                    break;

                case TerraformingBrushMode.PaintSurface:
                    break;
            }

            newH = math.clamp(newH, float2HeightLimits.x, float2HeightLimits.y);
            WriteHeights[globalIdx] = newH;

            // Evaluate local slope for automatic Mountain Cliff Rock blending (Blue channel)
            int leftX = math.max(0, gx - 1);
            int rightX = math.min(GlobalResolution - 1, gx + 1);
            int downZ = math.max(0, gz - 1);
            int upZ = math.min(GlobalResolution - 1, gz + 1);

            float dX = (ReadHeights[gz * GlobalResolution + rightX] - ReadHeights[gz * GlobalResolution + leftX]) / (2.0f * VertexSpacing);
            float dZ = (ReadHeights[upZ * GlobalResolution + gx] - ReadHeights[downZ * GlobalResolution + gx]) / (2.0f * VertexSpacing);
            float3 normal = math.normalize(new float3(-dX, 1.0f, -dZ));

            // Slope > ~35 degrees (normal.y < 0.82) blends into Cliff Rock
            float autoCliffFactor = math.saturate((0.84f - normal.y) / 0.22f);

            Color32 c = VertexColors[globalIdx];
            float4 weights = new float4(c.r, c.g, c.b, c.a) / 255.0f;

            if (BrushMode == TerraformingBrushMode.PaintSurface || AlsoPaintWhileSculpting)
            {
                float4 targetMask = float4.zero;
                switch (PaintLayer)
                {
                    case TerrainSurfaceLayer.TanSand: targetMask = new float4(1f, 0f, 0f, 0f); break;
                    case TerrainSurfaceLayer.GrassOrTurf: targetMask = new float4(0f, 1f, 0f, 0f); break;
                    case TerrainSurfaceLayer.CliffRock: targetMask = new float4(0f, 0f, 1f, 0f); break;
                    case TerrainSurfaceLayer.GardenPath: targetMask = new float4(0f, 0f, 0f, 1f); break;
                }

                float paintAlpha = math.saturate(BrushStrength * falloff * DeltaTime * 1.5f);
                weights = math.lerp(weights, targetMask, paintAlpha);
            }

            // Enforce automatic cliff rock on steep mountain faces and sandy beds below water level (-0.25m)
            if (BrushMode != TerraformingBrushMode.PaintSurface)
            {
                if (autoCliffFactor > 0.05f)
                {
                    weights = math.lerp(weights, new float4(0f, 0f, 1f, 0f), autoCliffFactor);
                }
                else if (newH < -0.20f)
                {
                    float underwaterSand = math.saturate((-0.20f - newH) / 0.35f);
                    weights = math.lerp(weights, new float4(1f, 0f, 0f, 0f), underwaterSand);
                }
            }

            // Normalize RGBA weights so they always sum to 1.0 (255)
            float weightSum = math.csum(weights);
            if (weightSum > 0.0001f)
            {
                weights /= weightSum;
            }
            else
            {
                weights = new float4(1f, 0f, 0f, 0f);
            }

            VertexColors[globalIdx] = new Color32(
                (byte)math.clamp(math.round(weights.x * 255f), 0f, 255f),
                (byte)math.clamp(math.round(weights.y * 255f), 0f, 255f),
                (byte)math.clamp(math.round(weights.z * 255f), 0f, 255f),
                (byte)math.clamp(math.round(weights.w * 255f), 0f, 255f)
            );
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    public struct ChunkMeshRebuildJob : IJobParallelFor
    {
        [ReadOnly] public int GlobalResolution;     // 257
        [ReadOnly] public int ChunkVertsPerSide;    // 33
        [ReadOnly] public int ChunkStartGlobalX;    // chunkCoordX * 32
        [ReadOnly] public int ChunkStartGlobalZ;    // chunkCoordZ * 32
        [ReadOnly] public float VertexSpacing;      // 0.25m

        [ReadOnly] public NativeArray<float> GlobalHeights;
        [ReadOnly] public NativeArray<Color32> GlobalColors;

        [WriteOnly] public NativeArray<float3> OutVertices;
        [WriteOnly] public NativeArray<float3> OutNormals;
        [WriteOnly] public NativeArray<Color32> OutColors;

        public void Execute(int localVertIndex)
        {
            int lx = localVertIndex % ChunkVertsPerSide;
            int lz = localVertIndex / ChunkVertsPerSide;

            int gx = ChunkStartGlobalX + lx;
            int gz = ChunkStartGlobalZ + lz;
            int globalIdx = gz * GlobalResolution + gx;

            float height = GlobalHeights[globalIdx];

            // Local chunk origin is at (0, 0, 0) in chunk space, spanning [0..8m]
            OutVertices[localVertIndex] = new float3(lx * VertexSpacing, height, lz * VertexSpacing);
            OutColors[localVertIndex] = GlobalColors[globalIdx];

            // Central-difference normal across global heightmap guarantees zero seam artifacts between chunks
            int leftX = math.max(0, gx - 1);
            int rightX = math.min(GlobalResolution - 1, gx + 1);
            int downZ = math.max(0, gz - 1);
            int upZ = math.min(GlobalResolution - 1, gz + 1);

            float hL = GlobalHeights[gz * GlobalResolution + leftX];
            float hR = GlobalHeights[gz * GlobalResolution + rightX];
            float hD = GlobalHeights[downZ * GlobalResolution + gx];
            float hU = GlobalHeights[upZ * GlobalResolution + gx];

            float dX = (hR - hL) / ((rightX - leftX) * VertexSpacing);
            float dZ = (hU - hD) / ((upZ - downZ) * VertexSpacing);

            OutNormals[localVertIndex] = math.normalize(new float3(-dX, 1.0f, -dZ));
        }
    }

    [BurstCompile]
    public struct DeferredColliderBakeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> MeshInstanceIds;

        public void Execute(int index)
        {
            Physics.BakeMesh(MeshInstanceIds[index], false);
        }
    }
}
