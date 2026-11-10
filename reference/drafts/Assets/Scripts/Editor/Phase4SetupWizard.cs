#if UNITY_EDITOR
using System.Collections.Generic;
using IslandBuilder.Foliage;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandBuilder.EditorTools
{
    public static class Phase4SetupWizard
    {
        private const string Phase4Folder = "Assets/Generated/Phase4";
        private const string LutPath = "Assets/Generated/Phase4/T_BotanicalPaletteLUT.asset";
        private const string MatPath = "Assets/Generated/Phase4/M_BotanicalVertexLUT.mat";
        private const string MeshesAssetPath = "Assets/Generated/Phase4/Phase4_Botanical_LOD_Meshes.asset";
        private const string CatalogAssetPath = "Assets/Generated/Phase4/BotanicalCatalogDatabase.asset";

        [MenuItem("IslandBuilder/Phase 4/1. Generate 398-Item Botanical Catalog, 4-Tier LOD Meshes & LUT", false, 30)]
        public static void GeneratePhase4BotanicalEngine()
        {
            EnsureFolders();

            Shader botShader = Shader.Find("IslandBuilder/BotanicalVertexLUT");
            if (botShader == null)
            {
                Debug.LogError("[Phase 4 Wizard] Could not find BotanicalVertexLUT.shader!");
                return;
            }

            // 1. Generate 2D Palette & Cultivar LUT Texture
            Texture2D lutTex = GenerateBotanicalPaletteLut(256, 64);

            // 2. Create GPU-Instanced Unified Botanical Material
            Material botMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (botMat == null)
            {
                botMat = new Material(botShader);
                AssetDatabase.CreateAsset(botMat, MatPath);
            }
            botMat.shader = botShader;
            botMat.SetTexture("_BotanicalPaletteLUT", lutTex);
            botMat.enableInstancing = true;
            EditorUtility.SetDirty(botMat);

            // 3. Generate Opaque Silhouette LOD Meshes (LOD0..LOD3) for Trees, Bushes, Flowers & Grass
            GenerateSharedBotanicalLodMeshes(
                out Mesh treeLod0, out Mesh treeLod1, out Mesh treeLod2, out Mesh treeLod3,
                out Mesh bushLod0, out Mesh bushLod1, out Mesh bushLod2,
                out Mesh flowerLod0, out Mesh flowerLod1, out Mesh flowerLod2,
                out Mesh grassLod0, out Mesh grassLod1);

            // 4. Populate BotanicalCatalogDatabase (36 Trees, 42 Bushes, 32x10=320 Flowers, 1 Grass)
            BotanicalCatalogDatabase db = AssetDatabase.LoadAssetAtPath<BotanicalCatalogDatabase>(CatalogAssetPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<BotanicalCatalogDatabase>();
                AssetDatabase.CreateAsset(db, CatalogAssetPath);
            }

            db.SharedBotanicalMaterial = botMat;
            db.BotanicalPaletteLutTexture = lutTex;
            PopulateCatalogEntries(
                db,
                treeLod0, treeLod1, treeLod2, treeLod3,
                bushLod0, bushLod1, bushLod2,
                flowerLod0, flowerLod1, flowerLod2,
                grassLod0, grassLod1);

            EditorUtility.SetDirty(db);

            // 5. Wire FoliageInstancingManager in active scene
            FoliageInstancingManager mgr = Object.FindFirstObjectByType<FoliageInstancingManager>();
            if (mgr == null)
            {
                GameObject go = new GameObject("_FoliageInstancingManager");
                mgr = go.AddComponent<FoliageInstancingManager>();
            }

            SerializedObject mgrSo = new SerializedObject(mgr);
            mgrSo.FindProperty("catalogDatabase").objectReferenceValue = db;
            mgrSo.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 4 Wizard] Generated 398-Variety Botanical Catalog (36 Trees, 42 Bushes, 320 Flowers + Grass), Opaque 4-Tier LOD Meshes, and wired FoliageInstancingManager!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Phase4Folder))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase4");
        }

        private static Texture2D GenerateBotanicalPaletteLut(int width, int height)
        {
            Texture2D lut = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            // Row 0 (V ~ 0.05): Bark -> Emerald Stem -> Lush Canopy Leaf Green
            Color bark = new Color(0.36f, 0.25f, 0.16f);
            Color deepLeaf = new Color(0.14f, 0.46f, 0.18f);
            Color brightLeaf = new Color(0.34f, 0.74f, 0.26f);

            // 10 Cultivar Palette pairs (Base -> Tip)
            (Color baseCol, Color tipCol)[] cultivars =
            {
                (new Color(0.75f, 0.08f, 0.15f), new Color(1.00f, 0.28f, 0.34f)), // 0: Ruby Crimson
                (new Color(0.90f, 0.52f, 0.05f), new Color(1.00f, 0.88f, 0.18f)), // 1: Sunset Gold
                (new Color(0.38f, 0.14f, 0.68f), new Color(0.68f, 0.42f, 0.96f)), // 2: Royal Violet
                (new Color(0.88f, 0.45f, 0.62f), new Color(1.00f, 0.82f, 0.88f)), // 3: Sakura Pink
                (new Color(0.10f, 0.32f, 0.82f), new Color(0.42f, 0.72f, 1.00f)), // 4: Cobalt Blue
                (new Color(0.85f, 0.88f, 0.84f), new Color(1.00f, 1.00f, 0.98f)), // 5: Pearl White
                (new Color(0.92f, 0.36f, 0.28f), new Color(1.00f, 0.74f, 0.56f)), // 6: Coral Peach
                (new Color(0.72f, 0.08f, 0.56f), new Color(0.98f, 0.38f, 0.84f)), // 7: Magenta Glow
                (new Color(0.86f, 0.18f, 0.22f), new Color(1.00f, 0.86f, 0.24f)), // 8: Bi-Color Sunrise
                (new Color(0.18f, 0.12f, 0.48f), new Color(0.46f, 0.58f, 0.94f))  // 9: Midnight Indigo
            };

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / (width - 1);
                    Color c;
                    if (v < 0.12f)
                    {
                        c = u < 0.35f
                            ? Color.Lerp(bark, deepLeaf, u / 0.35f)
                            : Color.Lerp(deepLeaf, brightLeaf, (u - 0.35f) / 0.65f);
                    }
                    else
                    {
                        int cultIdx = Mathf.Clamp(Mathf.FloorToInt(((v - 0.12f) / 0.88f) * 10f), 0, 9);
                        c = Color.Lerp(cultivars[cultIdx].baseCol, cultivars[cultIdx].tipCol, Mathf.SmoothStep(0f, 1f, u));
                    }
                    lut.SetPixel(x, y, c);
                }
            }

            lut.Apply(false, false);
            AssetDatabase.CreateAsset(lut, LutPath);
            return lut;
        }

        private static void GenerateSharedBotanicalLodMeshes(
            out Mesh treeLod0, out Mesh treeLod1, out Mesh treeLod2, out Mesh treeLod3,
            out Mesh bushLod0, out Mesh bushLod1, out Mesh bushLod2,
            out Mesh flowerLod0, out Mesh flowerLod1, out Mesh flowerLod2,
            out Mesh grassLod0, out Mesh grassLod1)
        {
            // Create container mesh asset
            treeLod0 = BuildOpaqueTreeMesh("Mesh_Tree_LOD0", radialSegments: 14, canopyLayers: 5, height: 5.5f, radius: 2.1f);
            treeLod1 = BuildOpaqueTreeMesh("Mesh_Tree_LOD1", radialSegments: 9, canopyLayers: 3, height: 5.5f, radius: 2.1f);
            treeLod2 = BuildOpaqueTreeMesh("Mesh_Tree_LOD2", radialSegments: 6, canopyLayers: 2, height: 5.5f, radius: 2.1f);
            treeLod3 = BuildOpaqueCrossBillboardMesh("Mesh_Tree_LOD3_Billboard", width: 4.0f, height: 5.5f, isPetal: false);

            bushLod0 = BuildOpaqueBushMesh("Mesh_Bush_LOD0", puffs: 7, segs: 8, radius: 0.85f);
            bushLod1 = BuildOpaqueBushMesh("Mesh_Bush_LOD1", puffs: 4, segs: 6, radius: 0.85f);
            bushLod2 = BuildOpaqueBushMesh("Mesh_Bush_LOD2", puffs: 2, segs: 5, radius: 0.85f);

            flowerLod0 = BuildOpaqueFlowerClusterMesh("Mesh_Flower_LOD0", blossoms: 5, petalsPerBlossom: 6);
            flowerLod1 = BuildOpaqueFlowerClusterMesh("Mesh_Flower_LOD1", blossoms: 3, petalsPerBlossom: 4);
            flowerLod2 = BuildOpaqueFlowerClusterMesh("Mesh_Flower_LOD2", blossoms: 1, petalsPerBlossom: 4);

            grassLod0 = BuildOpaqueGrassTuftMesh("Mesh_Grass_LOD0", bladeCount: 12);
            grassLod1 = BuildOpaqueGrassTuftMesh("Mesh_Grass_LOD1", bladeCount: 4);

            AssetDatabase.CreateAsset(treeLod0, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(treeLod1, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(treeLod2, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(treeLod3, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(bushLod0, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(bushLod1, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(bushLod2, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(flowerLod0, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(flowerLod1, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(flowerLod2, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(grassLod0, MeshesAssetPath);
            AssetDatabase.AddObjectToAsset(grassLod1, MeshesAssetPath);
        }

        private static Mesh BuildOpaqueTreeMesh(string name, int radialSegments, int canopyLayers, float height, float radius)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Color> cols = new List<Color>();
            List<int> tris = new List<int>();

            // Opaque Trunk Cone
            float trunkTopY = height * 0.45f;
            AppendConeSection(verts, norms, cols, tris, Vector3.zero, 0.28f, trunkTopY, 0.12f, radialSegments,
                gradMin: 0.02f, gradMax: 0.25f, partMask: 0.0f, aoBase: 0.35f, aoTop: 0.75f);

            // Layered Opaque Silhouette Canopy Cones
            for (int l = 0; l < canopyLayers; l++)
            {
                float t = (float)l / Mathf.Max(1, canopyLayers - 1);
                float baseY = Mathf.Lerp(height * 0.32f, height * 0.75f, t);
                float topY = baseY + (height * 0.32f);
                float layerRad = Mathf.Lerp(radius, radius * 0.45f, t);
                AppendConeSection(verts, norms, cols, tris, new Vector3(0f, baseY, 0f), layerRad, topY, 0.05f, radialSegments,
                    gradMin: 0.42f, gradMax: 0.98f, partMask: 0.0f, aoBase: 0.45f, aoTop: 1.0f);
            }

            return PackMesh(name, verts, norms, cols, tris);
        }

        private static Mesh BuildOpaqueBushMesh(string name, int puffs, int segs, float radius)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Color> cols = new List<Color>();
            List<int> tris = new List<int>();

            UnityEngine.Random.InitState(puffs * 17);
            for (int p = 0; p < puffs; p++)
            {
                Vector3 offset = p == 0
                    ? new Vector3(0f, radius * 0.45f, 0f)
                    : new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(0.2f, 0.55f), UnityEngine.Random.Range(-0.35f, 0.35f));
                float r = p == 0 ? radius * 0.65f : radius * 0.48f;
                AppendConeSection(verts, norms, cols, tris, new Vector3(offset.x, 0f, offset.z), r, offset.y + r, r * 0.2f, segs,
                    gradMin: 0.40f, gradMax: 0.95f, partMask: 0.0f, aoBase: 0.30f, aoTop: 1.0f);
            }

            return PackMesh(name, verts, norms, cols, tris);
        }

        private static Mesh BuildOpaqueFlowerClusterMesh(string name, int blossoms, int petalsPerBlossom)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Color> cols = new List<Color>();
            List<int> tris = new List<int>();

            UnityEngine.Random.InitState(blossoms * 31);
            for (int b = 0; b < blossoms; b++)
            {
                float angle = (b / (float)blossoms) * Mathf.PI * 2f;
                float dist = b == 0 ? 0f : 0.16f;
                Vector3 center = new Vector3(Mathf.Cos(angle) * dist, 0.28f + (b == 0 ? 0.06f : 0f), Mathf.Sin(angle) * dist);

                // Opaque Stem triangle prism
                AppendConeSection(verts, norms, cols, tris, new Vector3(center.x, 0f, center.z), 0.025f, center.y, 0.015f, 3,
                    gradMin: 0.38f, gradMax: 0.65f, partMask: 0.0f, aoBase: 0.35f, aoTop: 0.85f);

                // Opaque Geometry-Hugging Petals (partMask = 1.0 -> samples Cultivar LUT Row!)
                for (int p = 0; p < petalsPerBlossom; p++)
                {
                    float pAng = (p / (float)petalsPerBlossom) * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(pAng), 0.25f, Mathf.Sin(pAng)).normalized;
                    Vector3 side = new Vector3(-dir.z, 0f, dir.x) * 0.045f;

                    int baseIdx = verts.Count;
                    verts.Add(center);
                    verts.Add(center + dir * 0.07f + side);
                    verts.Add(center + dir * 0.13f);
                    verts.Add(center + dir * 0.07f - side);

                    for (int k = 0; k < 4; k++) norms.Add(Vector3.up);
                    cols.Add(new Color(0.10f, 1.0f, 0.85f, 0.5f));
                    cols.Add(new Color(0.55f, 1.0f, 0.95f, 0.8f));
                    cols.Add(new Color(0.98f, 1.0f, 1.00f, 1.0f));
                    cols.Add(new Color(0.55f, 1.0f, 0.95f, 0.8f));

                    tris.Add(baseIdx + 0); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                    tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
                }
            }

            return PackMesh(name, verts, norms, cols, tris);
        }

        private static Mesh BuildOpaqueGrassTuftMesh(string name, int bladeCount)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Color> cols = new List<Color>();
            List<int> tris = new List<int>();

            UnityEngine.Random.InitState(bladeCount * 19);
            for (int i = 0; i < bladeCount; i++)
            {
                float ang = (i / (float)bladeCount) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
                Vector3 offset = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * UnityEngine.Random.Range(0.03f, 0.18f);
                Vector3 lean = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.09f;
                Vector3 side = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 0.022f;
                float h = UnityEngine.Random.Range(0.18f, 0.34f);

                int idx = verts.Count;
                verts.Add(offset - side);
                verts.Add(offset + side);
                verts.Add(offset + lean + new Vector3(0f, h, 0f));

                Vector3 n = Vector3.Normalize(Vector3.up + lean);
                norms.Add(n); norms.Add(n); norms.Add(Vector3.up);

                // Root AO darkened (0.35), tip bright (1.0)
                cols.Add(new Color(0.42f, 0.0f, 0.35f, 0.1f));
                cols.Add(new Color(0.42f, 0.0f, 0.35f, 0.1f));
                cols.Add(new Color(0.95f, 0.0f, 1.00f, 0.8f));

                tris.Add(idx + 0); tris.Add(idx + 1); tris.Add(idx + 2);
            }

            return PackMesh(name, verts, norms, cols, tris);
        }

        private static Mesh BuildOpaqueCrossBillboardMesh(string name, float width, float height, bool isPetal)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Color> cols = new List<Color>();
            List<int> tris = new List<int>();

            float hw = width * 0.5f;
            // Two intersecting opaque diamond silhouettes (8 triangles total)
            for (int plane = 0; plane < 2; plane++)
            {
                Vector3 right = plane == 0 ? new Vector3(hw, 0f, 0f) : new Vector3(0f, 0f, hw);
                int b = verts.Count;
                verts.Add(new Vector3(0f, 0f, 0f));
                verts.Add(right + new Vector3(0f, height * 0.45f, 0f));
                verts.Add(new Vector3(0f, height, 0f));
                verts.Add(-right + new Vector3(0f, height * 0.45f, 0f));

                for (int k = 0; k < 4; k++) norms.Add(Vector3.up);
                float mask = isPetal ? 1.0f : 0.0f;
                cols.Add(new Color(0.35f, mask, 0.4f, 0.1f));
                cols.Add(new Color(0.65f, mask, 0.8f, 0.4f));
                cols.Add(new Color(0.95f, mask, 1.0f, 0.8f));
                cols.Add(new Color(0.65f, mask, 0.8f, 0.4f));

                tris.Add(b + 0); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 3);
                tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 0); tris.Add(b + 3); tris.Add(b + 2);
            }

            return PackMesh(name, verts, norms, cols, tris);
        }

        private static void AppendConeSection(
            List<Vector3> verts, List<Vector3> norms, List<Color> cols, List<int> tris,
            Vector3 bottomCenter, float bottomRad, float topY, float topRad, int segs,
            float gradMin, float gradMax, float partMask, float aoBase, float aoTop)
        {
            int startIdx = verts.Count;
            for (int i = 0; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(bottomCenter + dir * bottomRad);
                verts.Add(new Vector3(bottomCenter.x, topY, bottomCenter.z) + dir * topRad);

                Vector3 n = Vector3.Normalize(dir + Vector3.up * 0.35f);
                norms.Add(n);
                norms.Add(n);

                cols.Add(new Color(gradMin, partMask, aoBase, 0.2f));
                cols.Add(new Color(gradMax, partMask, aoTop, 0.7f));
            }

            for (int i = 0; i < segs; i++)
            {
                int bl = startIdx + i * 2;
                int tl = bl + 1;
                int br = bl + 2;
                int tr = bl + 3;

                tris.Add(bl); tris.Add(tl); tris.Add(tr);
                tris.Add(bl); tris.Add(tr); tris.Add(br);
            }
        }

        private static Mesh PackMesh(string name, List<Vector3> verts, List<Vector3> norms, List<Color> cols, List<int> tris)
        {
            Mesh m = new Mesh
            {
                name = name,
                indexFormat = IndexFormat.UInt16
            };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        private static void PopulateCatalogEntries(
            BotanicalCatalogDatabase db,
            Mesh treeLod0, Mesh treeLod1, Mesh treeLod2, Mesh treeLod3,
            Mesh bushLod0, Mesh bushLod1, Mesh bushLod2,
            Mesh flowerLod0, Mesh flowerLod1, Mesh flowerLod2,
            Mesh grassLod0, Mesh grassLod1)
        {
            db.MeshArchetypes = new BotanicalMeshEntry[BotanicalCatalogDatabase.TotalMeshArchetypes];

            // 1. Populate 36 Tree Species (Archetypes 0..35)
            for (int i = 0; i < BotanicalCatalogDatabase.TreeSpeciesCount; i++)
            {
                float hueShift = (i % 7) * 0.03f;
                db.MeshArchetypes[i] = new BotanicalMeshEntry
                {
                    SpeciesName = BotanicalCatalogDatabase.All36TreeNames[i],
                    MeshArchetypeId = i,
                    Category = BotanicalCategory.Tree,
                    Lod0Mesh = treeLod0,
                    Lod1Mesh = treeLod1,
                    Lod2Mesh = treeLod2,
                    Lod3BillboardMesh = treeLod3,
                    Lod0To1Distance = 14.0f,
                    Lod1To2Distance = 26.0f,
                    Lod2To3Distance = 42.0f,
                    MaxCullDistance = 120.0f,
                    BoundingSphereRadius = 3.2f,
                    DefaultMinSpacingMeters = 2.8f,
                    BaseTint = new Vector3(1.0f - hueShift, 1.0f, 1.0f - hueShift * 0.5f)
                };
            }

            // 2. Populate 42 Bush Species (Archetypes 36..77)
            for (int i = 0; i < BotanicalCatalogDatabase.BushSpeciesCount; i++)
            {
                int id = 36 + i;
                float tintVar = (i % 5) * 0.035f;
                db.MeshArchetypes[id] = new BotanicalMeshEntry
                {
                    SpeciesName = BotanicalCatalogDatabase.All42BushNames[i],
                    MeshArchetypeId = id,
                    Category = BotanicalCategory.BushOrShrub,
                    Lod0Mesh = bushLod0,
                    Lod1Mesh = bushLod1,
                    Lod2Mesh = bushLod2,
                    Lod3BillboardMesh = bushLod2,
                    Lod0To1Distance = 10.0f,
                    Lod1To2Distance = 20.0f,
                    Lod2To3Distance = 34.0f,
                    MaxCullDistance = 85.0f,
                    BoundingSphereRadius = 1.1f,
                    DefaultMinSpacingMeters = 1.15f,
                    BaseTint = new Vector3(1.0f - tintVar, 1.0f, 1.0f)
                };
            }

            // 3. Populate 32 Base Flower Archetypes (Archetypes 78..109 -> x 10 Cultivars = 320 Varieties)
            for (int i = 0; i < BotanicalCatalogDatabase.BaseFlowerMeshCount; i++)
            {
                int id = 78 + i;
                db.MeshArchetypes[id] = new BotanicalMeshEntry
                {
                    SpeciesName = BotanicalCatalogDatabase.All32BaseFlowerNames[i],
                    MeshArchetypeId = id,
                    Category = BotanicalCategory.FlowerCluster,
                    Lod0Mesh = flowerLod0,
                    Lod1Mesh = flowerLod1,
                    Lod2Mesh = flowerLod2,
                    Lod3BillboardMesh = flowerLod2,
                    Lod0To1Distance = 7.0f,
                    Lod1To2Distance = 14.0f,
                    Lod2To3Distance = 22.0f,
                    MaxCullDistance = 32.0f,
                    BoundingSphereRadius = 0.35f,
                    DefaultMinSpacingMeters = 0.45f,
                    BaseTint = Vector3.one
                };
            }

            // 4. Populate Natural Grass Blade Cluster (Archetype 110)
            db.MeshArchetypes[BotanicalCatalogDatabase.GrassArchetypeIndex] = new BotanicalMeshEntry
            {
                SpeciesName = "Natural Grass Blade Cluster",
                MeshArchetypeId = BotanicalCatalogDatabase.GrassArchetypeIndex,
                Category = BotanicalCategory.NaturalGrass,
                Lod0Mesh = grassLod0,
                Lod1Mesh = grassLod1,
                Lod2Mesh = grassLod1,
                Lod3BillboardMesh = grassLod1,
                Lod0To1Distance = 8.0f,
                Lod1To2Distance = 15.0f,
                Lod2To3Distance = 20.0f,
                MaxCullDistance = 22.0f,
                BoundingSphereRadius = 0.30f,
                DefaultMinSpacingMeters = 0.40f,
                BaseTint = Vector3.one
            };

            // 5. Populate 10 Flower Cultivar LUT Rows
            string[] cultivarNames =
            {
                "Ruby Crimson", "Sunset Gold", "Royal Violet", "Sakura Pink", "Cobalt Blue",
                "Pearl White", "Coral Peach", "Magenta Glow", "Bi-Color Sunrise", "Midnight Indigo"
            };

            db.FlowerCultivars = new FlowerCultivarPalette[BotanicalCatalogDatabase.CultivarsPerFlower];
            for (int c = 0; c < BotanicalCatalogDatabase.CultivarsPerFlower; c++)
            {
                db.FlowerCultivars[c] = new FlowerCultivarPalette
                {
                    CultivarName = cultivarNames[c],
                    LutRowCoordinateV = 0.16f + c * 0.088f
                };
            }
        }
    }
}
#endif
