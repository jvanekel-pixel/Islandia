#if UNITY_EDITOR
using IslandBuilder.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace IslandBuilder.EditorTools
{
    public static class Phase2SetupWizard
    {
        private const string GeneratedFolderPath = "Assets/Generated/Phase2";
        private const string ArrayAssetPath = "Assets/Generated/Phase2/T_IslandTerrainAlbedoArray.asset";
        private const string LutAssetPath = "Assets/Generated/Phase2/T_IslandShadingAndTurfLUT.asset";
        private const string TerrainMatPath = "Assets/Generated/Phase2/M_IslandTerrainUber.mat";
        private const string WaterMatPath = "Assets/Generated/Phase2/M_VulkanWaterDepthFoam.mat";

        [MenuItem("IslandBuilder/Phase 2/1. Generate Terrain Texture2DArray, LUT & Materials", false, 10)]
        public static void GeneratePhase2AssetsAndScene()
        {
            EnsureFolders();

            Texture2DArray albedoArray = GenerateDefaultTerrainTextureArray(256);
            Texture2D shadingLut = GenerateShadingAndAstroturfLut(256, 32);

            Shader terrainShader = Shader.Find("IslandBuilder/IslandTerrainUber");
            Shader waterShader = Shader.Find("IslandBuilder/VulkanWaterDepthFoam");

            if (terrainShader == null || waterShader == null)
            {
                Debug.LogError("[Phase 2 Wizard] Could not find Phase 2 shaders. Ensure IslandTerrainUber.shader and VulkanWaterDepthFoam.shader are compiled.");
                return;
            }

            Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (terrainMat == null)
            {
                terrainMat = new Material(terrainShader);
                AssetDatabase.CreateAsset(terrainMat, TerrainMatPath);
            }
            terrainMat.shader = terrainShader;
            terrainMat.SetTexture("_TerrainAlbedoArray", albedoArray);
            terrainMat.SetTexture("_ShadingAndTurfLut", shadingLut);
            terrainMat.enableInstancing = true;
            EditorUtility.SetDirty(terrainMat);

            Material waterMat = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            if (waterMat == null)
            {
                waterMat = new Material(waterShader);
                AssetDatabase.CreateAsset(waterMat, WaterMatPath);
            }
            waterMat.shader = waterShader;
            waterMat.enableInstancing = true;
            EditorUtility.SetDirty(waterMat);

            // Automatically wire IslandTerrainManager in the active scene
            IslandTerrainManager manager = Object.FindFirstObjectByType<IslandTerrainManager>();
            if (manager == null)
            {
                GameObject go = new GameObject("_IslandTerrainManager");
                manager = go.AddComponent<IslandTerrainManager>();
                go.AddComponent<VRTerraformingBrushController>();
            }

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("terrainUberMaterial").objectReferenceValue = terrainMat;
            so.FindProperty("vulkanWaterMaterial").objectReferenceValue = waterMat;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 2 Wizard] Generated Texture2DArray, LUT, Materials, and wired IslandTerrainManager!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(GeneratedFolderPath))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase2");
        }

        private static Texture2DArray GenerateDefaultTerrainTextureArray(int size)
        {
            Texture2DArray array = new Texture2DArray(size, size, 4, TextureFormat.RGBA32, true, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };

            // Slice 0: Warm Tan Sand
            FillProceduralSlice(array, 0, size, new Color(0.87f, 0.79f, 0.61f), new Color(0.79f, 0.70f, 0.52f), 18f);
            // Slice 1: Vibrant Lush Grass / Astroturf Base
            FillProceduralSlice(array, 1, size, new Color(0.24f, 0.64f, 0.26f), new Color(0.18f, 0.52f, 0.20f), 24f);
            // Slice 2: Mountain Cliff Slate Rock
            FillProceduralSlice(array, 2, size, new Color(0.46f, 0.45f, 0.43f), new Color(0.32f, 0.31f, 0.30f), 12f);
            // Slice 3: Warm Stone Garden Path
            FillProceduralSlice(array, 3, size, new Color(0.68f, 0.64f, 0.58f), new Color(0.52f, 0.49f, 0.44f), 32f);

            array.Apply(true, false);
            AssetDatabase.CreateAsset(array, ArrayAssetPath);
            return array;
        }

        private static void FillProceduralSlice(Texture2DArray array, int slice, int size, Color cA, Color cB, float freq)
        {
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / size * freq;
                    float ny = (float)y / size * freq;
                    // Seamless tiling Perlin approximation
                    float n = Mathf.PerlinNoise(nx, ny) * 0.65f + Mathf.PerlinNoise(nx * 2.3f, ny * 2.3f) * 0.35f;
                    pixels[y * size + x] = Color.Lerp(cA, cB, n);
                }
            }
            array.SetPixels(pixels, slice, 0);
        }

        private static Texture2D GenerateShadingAndAstroturfLut(int width, int height)
        {
            Texture2D lut = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color shadowTint = new Color(0.42f, 0.46f, 0.58f); // Cool ambient skylight shadow
            Color midWarm = new Color(0.92f, 0.88f, 0.82f);
            Color sunLit = new Color(1.00f, 0.99f, 0.95f);
            Color turfSheen = new Color(0.55f, 0.95f, 0.45f);

            for (int y = 0; y < height; y++)
            {
                bool isTurfRow = y >= (height / 2);
                for (int x = 0; x < width; x++)
                {
                    float t = (float)x / (width - 1);
                    if (!isTurfRow)
                    {
                        float smooth = Mathf.SmoothStep(0f, 1f, t);
                        Color ramp = t < 0.5f
                            ? Color.Lerp(shadowTint, midWarm, smooth * 2f)
                            : Color.Lerp(midWarm, sunLit, (smooth - 0.5f) * 2f);
                        lut.SetPixel(x, y, ramp);
                    }
                    else
                    {
                        lut.SetPixel(x, y, Color.Lerp(Color.black, turfSheen, Mathf.Pow(t, 1.8f)));
                    }
                }
            }

            lut.Apply(false, false);
            AssetDatabase.CreateAsset(lut, LutAssetPath);
            return lut;
        }
    }
}
#endif
