#if UNITY_EDITOR
using System.Collections.Generic;
using IslandBuilder.Architecture;
using IslandBuilder.UX;
using UnityEditor;
using UnityEngine;

namespace IslandBuilder.EditorTools
{
    public static class Phase5SetupWizard
    {
        private const string Phase5Folder = "Assets/Generated/Phase5";
        private const string AtlasPath = "Assets/Generated/Phase5/T_ArchitecturalAtlas.asset";
        private const string MatCapPath = "Assets/Generated/Phase5/T_GlassMatCap.asset";
        private const string MatPath = "Assets/Generated/Phase5/M_ArchitecturalAtlasUber.mat";
        private const string MeshesPath = "Assets/Generated/Phase5/Phase5_Architectural_LOD_Meshes.asset";
        private const string CatalogPath = "Assets/Generated/Phase5/ArchitecturalCatalogDatabase.asset";
        private const string PrefabsFolder = "Assets/Generated/Phase5/Prefabs";

        [MenuItem("IslandBuilder/Phase 5/1. Generate 233-Item Architectural Catalog, MatCap Glass Atlas & Welder", false, 40)]
        public static void GeneratePhase5ArchitectureSuite()
        {
            EnsureFolders();

            Shader archShader = Shader.Find("IslandBuilder/ArchitecturalAtlasUber");
            if (archShader == null)
            {
                Debug.LogError("[Phase 5 Wizard] Could not find ArchitecturalAtlasUber.shader!");
                return;
            }

            // 1. Generate Shared Architectural Atlas & Opaque Glass MatCap Texture
            Texture2D atlasTex = GenerateArchitecturalAtlasTexture(512);
            Texture2D matCapTex = GenerateGlassMatCapTexture(256);

            // 2. Create Shared Unified Material
            Material archMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (archMat == null)
            {
                archMat = new Material(archShader);
                AssetDatabase.CreateAsset(archMat, MatPath);
            }
            archMat.shader = archShader;
            archMat.SetTexture("_ArchAtlasMap", atlasTex);
            archMat.SetTexture("_GlassMatCapTex", matCapTex);
            archMat.enableInstancing = true;
            EditorUtility.SetDirty(archMat);

            // 3. Generate Representative Multi-LOD Meshes (Gazebos, Conservatory, Modules, Rocks, Glowing Lanterns, Media Props)
            GenerateSharedArchitecturalMeshes(
                out Mesh gazeboLod0, out Mesh gazeboLod2,
                out Mesh conservatoryLod0,
                out Mesh wallModuleLod0, out Mesh wallWindowLod0,
                out Mesh rockLod0, out Mesh glowingLanternLod0, out Mesh mediaPropLod0);

            // 4. Populate 233-Item ArchitecturalCatalogDatabase & Representative Prefabs
            ArchitecturalCatalogDatabase db = AssetDatabase.LoadAssetAtPath<ArchitecturalCatalogDatabase>(CatalogPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ArchitecturalCatalogDatabase>();
                AssetDatabase.CreateAsset(db, CatalogPath);
            }

            db.SharedArchitecturalMaterial = archMat;
            db.SharedArchitecturalAtlas = atlasTex;
            db.SharedGlassMatCapTexture = matCapTex;

            PopulateAll233CatalogItems(
                db, archMat,
                gazeboLod0, gazeboLod2, conservatoryLod0,
                wallModuleLod0, wallWindowLod0,
                rockLod0, glowingLanternLod0, mediaPropLod0);

            EditorUtility.SetDirty(db);

            // 5. Wire RuntimeMeshWelder & IslandGltfExporter in Active Scene
            GameObject archSystemGo = GameObject.Find("_Phase5_Architecture_System");
            if (archSystemGo == null) archSystemGo = new GameObject("_Phase5_Architecture_System");

            var welder = archSystemGo.GetComponent<RuntimeMeshWelder>();
            if (welder == null) welder = archSystemGo.AddComponent<RuntimeMeshWelder>();

            SerializedObject welderSo = new SerializedObject(welder);
            welderSo.FindProperty("sharedArchitecturalMaterial").objectReferenceValue = archMat;
            welderSo.ApplyModifiedProperties();

            if (archSystemGo.GetComponent<IslandGltfExporter>() == null)
            {
                archSystemGo.AddComponent<IslandGltfExporter>();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 5 Wizard] Generated 233-Item Architectural Catalog (28 Gazebos, 95 Modules, 110 Props), Opaque MatCap Glass Atlas, RuntimeMeshWelder, and IslandGltfExporter!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Phase5Folder))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase5");
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
                AssetDatabase.CreateFolder(Phase5Folder, "Prefabs");
        }

        private static Texture2D GenerateArchitecturalAtlasTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            // 4 Quadrants: Warm Cedar/Teak Wood, Mediterranean Stucco/Concrete, Natural Slate/Granite Stone, Roof Terracotta/Thatch
            Color wood = new Color(0.68f, 0.46f, 0.28f);
            Color stucco = new Color(0.92f, 0.89f, 0.83f);
            Color stone = new Color(0.52f, 0.54f, 0.56f);
            Color roof = new Color(0.76f, 0.36f, 0.24f);

            int half = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color baseCol = (y < half)
                        ? (x < half ? wood : stucco)
                        : (x < half ? stone : roof);
                    float grain = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.14f - 0.07f;
                    tex.SetPixel(x, y, baseCol + new Color(grain, grain, grain, 0f));
                }
            }

            tex.Apply(true, false);
            AssetDatabase.CreateAsset(tex, AtlasPath);
            return tex;
        }

        private static Texture2D GenerateGlassMatCapTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color deepSky = new Color(0.18f, 0.42f, 0.68f);
            Color horizonCyan = new Color(0.62f, 0.88f, 0.96f);
            Color sunSpec = new Color(1.00f, 0.99f, 0.94f);

            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float fresnel = Mathf.Clamp01(r);
                    Color sky = Color.Lerp(deepSky, horizonCyan, Mathf.Clamp01(ny * 0.5f + 0.5f));
                    // Crisp diagonal sun reflection streak across the MatCap sphere
                    float streak = Mathf.Pow(Mathf.Clamp01(1.0f - Mathf.Abs(nx + ny - 0.35f) * 2.5f), 4.0f);
                    Color finalCol = Color.Lerp(sky, sunSpec, Mathf.Clamp01(streak + fresnel * 0.35f));
                    tex.SetPixel(x, y, finalCol);
                }
            }

            tex.Apply(false, false);
            AssetDatabase.CreateAsset(tex, MatCapPath);
            return tex;
        }

        private static void GenerateSharedArchitecturalMeshes(
            out Mesh gazeboLod0, out Mesh gazeboLod2,
            out Mesh conservatoryLod0,
            out Mesh wallModuleLod0, out Mesh wallWindowLod0,
            out Mesh rockLod0, out Mesh glowingLanternLod0, out Mesh mediaPropLod0)
        {
            gazeboLod0 = BuildOctagonalGazeboMesh("Mesh_Gazebo_LOD0", sides: 8, includeOpaqueGlass: false);
            gazeboLod2 = BuildOctagonalGazeboMesh("Mesh_Gazebo_LOD2", sides: 6, includeOpaqueGlass: false);
            conservatoryLod0 = BuildOctagonalGazeboMesh("Mesh_GlassConservatory_MatCap_LOD0", sides: 8, includeOpaqueGlass: true);

            wallModuleLod0 = BuildWallModuleMesh("Mesh_WallModule_Solid_LOD0", hasMatCapWindow: false);
            wallWindowLod0 = BuildWallModuleMesh("Mesh_WallModule_MatCapWindow_LOD0", hasMatCapWindow: true);

            rockLod0 = BuildArchitecturalBoxMesh("Mesh_RockOutcrop_LOD0", new Vector3(1.6f, 1.1f, 1.4f), new Vector2(0.25f, 0.75f), glassMask: 0f, emissiveMask: 0f);
            glowingLanternLod0 = BuildArchitecturalBoxMesh("Mesh_GlowingLantern_LOD0", new Vector3(0.45f, 1.1f, 0.45f), new Vector2(0.25f, 0.75f), glassMask: 0f, emissiveMask: 0.95f);
            mediaPropLod0 = BuildArchitecturalBoxMesh("Mesh_LiveMediaProp_LOD0", new Vector3(0.65f, 0.42f, 0.28f), new Vector2(0.75f, 0.25f), glassMask: 0.35f, emissiveMask: 0f);

            AssetDatabase.CreateAsset(gazeboLod0, MeshesPath);
            AssetDatabase.AddObjectToAsset(gazeboLod2, MeshesPath);
            AssetDatabase.AddObjectToAsset(conservatoryLod0, MeshesPath);
            AssetDatabase.AddObjectToAsset(wallModuleLod0, MeshesPath);
            AssetDatabase.AddObjectToAsset(wallWindowLod0, MeshesPath);
            AssetDatabase.AddObjectToAsset(rockLod0, MeshesPath);
            AssetDatabase.AddObjectToAsset(glowingLanternLod0, MeshesPath);
            AssetDatabase.AddObjectToAsset(mediaPropLod0, MeshesPath);
        }

        private static Mesh BuildOctagonalGazeboMesh(string name, int sides, bool includeOpaqueGlass)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> cols = new List<Color>();
            List<int> tris = new List<int>();

            float radius = 2.4f;
            float roofY = 2.6f;
            float peakY = 3.8f;

            // Plinth Base + Support Pillars + Roof Dome
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);

                // Roof segment triangle
                int rIdx = verts.Count;
                verts.Add(p0 + Vector3.up * roofY);
                verts.Add(p1 + Vector3.up * roofY);
                verts.Add(new Vector3(0f, peakY, 0f));

                Vector3 rNorm = Vector3.Normalize((p0 + p1) * 0.5f + Vector3.up * 1.5f);
                norms.Add(rNorm); norms.Add(rNorm); norms.Add(Vector3.up);
                uvs.Add(new Vector2(0.75f, 0.75f)); uvs.Add(new Vector2(0.95f, 0.75f)); uvs.Add(new Vector2(0.85f, 0.95f));

                // If Glass Conservatory, roof and side panels use Opaque MatCap Glass (color.g = 1.0)
                float glassWeight = includeOpaqueGlass ? 0.92f : 0.0f;
                cols.Add(new Color(1f, glassWeight, 0f, 0.85f));
                cols.Add(new Color(1f, glassWeight, 0f, 0.85f));
                cols.Add(new Color(1f, glassWeight, 0f, 1.00f));

                tris.Add(rIdx); tris.Add(rIdx + 2); tris.Add(rIdx + 1);

                // If Conservatory, add Opaque MatCap Glass wall panes on 7 of 8 sides (leaving 1 open entry)
                if (includeOpaqueGlass && i > 0)
                {
                    int wIdx = verts.Count;
                    verts.Add(p0);
                    verts.Add(p1);
                    verts.Add(p1 + Vector3.up * roofY);
                    verts.Add(p0 + Vector3.up * roofY);

                    Vector3 wNorm = Vector3.Normalize(p0 + p1);
                    for (int k = 0; k < 4; k++)
                    {
                        norms.Add(wNorm);
                        uvs.Add(new Vector2(0.75f, 0.25f));
                        cols.Add(new Color(1f, 1.0f, 0f, 0.95f)); // color.g = 1.0 -> Opaque MatCap Glass!
                    }
                    tris.Add(wIdx); tris.Add(wIdx + 2); tris.Add(wIdx + 1);
                    tris.Add(wIdx); tris.Add(wIdx + 3); tris.Add(wIdx + 2);
                }
            }

            return PackArchitecturalMesh(name, verts, norms, uvs, cols, tris);
        }

        private static Mesh BuildWallModuleMesh(string name, bool hasMatCapWindow)
        {
            // 2m wide x 3m tall x 0.25m thick modular building wall
            Mesh m = BuildArchitecturalBoxMesh(name, new Vector3(2.0f, 3.0f, 0.25f), new Vector2(0.75f, 0.25f),
                glassMask: hasMatCapWindow ? 0.85f : 0.0f, emissiveMask: 0.0f);
            return m;
        }

        private static Mesh BuildArchitecturalBoxMesh(string name, Vector3 size, Vector2 atlasQuadrantUv, float glassMask, float emissiveMask)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh src = temp.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] srcVerts = src.vertices;
            Vector3[] srcNorms = src.normals;
            int[] srcTris = src.triangles;
            Object.DestroyImmediate(temp);

            List<Vector3> verts = new List<Vector3>(srcVerts.Length);
            List<Vector3> norms = new List<Vector3>(srcNorms);
            List<Vector2> uvs = new List<Vector2>(srcVerts.Length);
            List<Color> cols = new List<Color>(srcVerts.Length);

            for (int i = 0; i < srcVerts.Length; i++)
            {
                Vector3 v = new Vector3(srcVerts[i].x * size.x, (srcVerts[i].y + 0.5f) * size.y, srcVerts[i].z * size.z);
                verts.Add(v);
                uvs.Add(atlasQuadrantUv);
                float ao = v.y < 0.1f ? 0.45f : 1.0f;
                cols.Add(new Color(1.0f, glassMask, emissiveMask, ao));
            }

            return PackArchitecturalMesh(name, verts, norms, uvs, cols, new List<int>(srcTris));
        }

        private static Mesh PackArchitecturalMesh(
            string name, List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<Color> cols, List<int> tris)
        {
            Mesh m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        private static void PopulateAll233CatalogItems(
            ArchitecturalCatalogDatabase db,
            Material archMat,
            Mesh gazeboLod0, Mesh gazeboLod2, Mesh conservatoryLod0,
            Mesh wallModuleLod0, Mesh wallWindowLod0,
            Mesh rockLod0, Mesh glowingLanternLod0, Mesh mediaPropLod0)
        {
            db.Items = new ArchitecturalCatalogItem[ArchitecturalCatalogDatabase.TotalArchitecturalItems];

            // Create 4 core reusable prefabs in Assets/Generated/Phase5/Prefabs for immediate Toybox/Studio placement
            GameObject gazeboPrefab = CreateReusableArchPrefab("Prefab_Gazebo_Standard", gazeboLod0, archMat, canWeld: true);
            GameObject conservatoryPrefab = CreateReusableArchPrefab("Prefab_Glass_Conservatory_MatCap", conservatoryLod0, archMat, canWeld: true);
            GameObject wallModulePrefab = CreateReusableArchPrefab("Prefab_Modular_Wall_2x3m", wallModuleLod0, archMat, canWeld: true);
            GameObject lanternPrefab = CreateReusableArchPrefab("Prefab_Glowing_Lantern_Prop", glowingLanternLod0, archMat, canWeld: true);

            int index = 0;

            // 1. 28 Gazebos, Pergolas & Pavilions (IDs 0..27)
            for (int g = 0; g < ArchitecturalCatalogDatabase.GazeboCount; g++)
            {
                string gName = ArchitecturalCatalogDatabase.All28GazeboNames[g];
                bool isMatCapGlass = gName.Contains("Conservatory") || gName.Contains("Dome") || gName.Contains("Observatory");

                db.Items[index] = new ArchitecturalCatalogItem
                {
                    DisplayName = gName,
                    CatalogId = index,
                    Category = ArchitecturalCategory.GazeboOrPavilion,
                    Theme = ArchitecturalTheme.UniversalPropOrGazebo,
                    ModuleSubType = StructuralModuleSubType.None,
                    Prefab = isMatCapGlass ? conservatoryPrefab : gazeboPrefab,
                    Lod0Mesh = isMatCapGlass ? conservatoryLod0 : gazeboLod0,
                    Lod1Mesh = gazeboLod2,
                    Lod2Mesh = gazeboLod2,
                    FoundationRadiusMeters = 3.2f,
                    AutoLevelTerrainOnPlace = true,
                    CanWeldIntoStaticAssembly = true,
                    UsesOpaqueMatCapGlass = isMatCapGlass,
                    HasBakedVertexEmissiveGlow = gName.Contains("Lantern") || gName.Contains("Fire-Pit")
                };
                index++;
            }

            // 2. 95 Modular Buildings (5 Themes x 19 Structural Modules = IDs 28..122)
            for (int themeIdx = 0; themeIdx < 5; themeIdx++)
            {
                ArchitecturalTheme theme = (ArchitecturalTheme)(themeIdx + 1);
                string themeName = ArchitecturalCatalogDatabase.All5ThemeNames[themeIdx];

                for (int modIdx = 0; modIdx < 19; modIdx++)
                {
                    StructuralModuleSubType subType = (StructuralModuleSubType)(modIdx + 1);
                    string modName = $"{themeName} - {ArchitecturalCatalogDatabase.All19ModuleTypeNames[modIdx]}";
                    bool isWindow = subType == StructuralModuleSubType.WindowWallOpaqueMatCap;

                    db.Items[index] = new ArchitecturalCatalogItem
                    {
                        DisplayName = modName,
                        CatalogId = index,
                        Category = ArchitecturalCategory.ModularBuilding,
                        Theme = theme,
                        ModuleSubType = subType,
                        Prefab = wallModulePrefab,
                        Lod0Mesh = isWindow ? wallWindowLod0 : wallModuleLod0,
                        Lod1Mesh = wallModuleLod0,
                        Lod2Mesh = wallModuleLod0,
                        FoundationRadiusMeters = 1.5f,
                        AutoLevelTerrainOnPlace = (subType == StructuralModuleSubType.FoundationBlock),
                        CanWeldIntoStaticAssembly = true,
                        UsesOpaqueMatCapGlass = isWindow,
                        HasBakedVertexEmissiveGlow = (subType == StructuralModuleSubType.ZenBridgeOrLighthouse)
                    };
                    index++;
                }
            }

            // 3. 20 Rocks & Boulders (IDs 123..142)
            for (int r = 0; r < 20; r++)
            {
                db.Items[index] = new ArchitecturalCatalogItem
                {
                    DisplayName = ArchitecturalCatalogDatabase.All20RockNames[r],
                    CatalogId = index,
                    Category = ArchitecturalCategory.RockOrBoulder,
                    Theme = ArchitecturalTheme.UniversalPropOrGazebo,
                    Prefab = lanternPrefab,
                    Lod0Mesh = rockLod0,
                    Lod1Mesh = rockLod0,
                    Lod2Mesh = rockLod0,
                    FoundationRadiusMeters = 1.0f,
                    AutoLevelTerrainOnPlace = false,
                    CanWeldIntoStaticAssembly = true
                };
                index++;
            }

            // 4. 75 Outdoor Living Props (IDs 143..217)
            for (int p = 0; p < 75; p++)
            {
                bool isGlowingProp = (p % 6 == 0); // Fire Pits, Tiki Torches, Stone Lanterns
                db.Items[index] = new ArchitecturalCatalogItem
                {
                    DisplayName = isGlowingProp ? $"Outdoor Lantern / Torch Variant #{p + 1}" : $"Outdoor Living Furniture / Prop #{p + 1}",
                    CatalogId = index,
                    Category = ArchitecturalCategory.OutdoorLivingProp,
                    Theme = ArchitecturalTheme.UniversalPropOrGazebo,
                    Prefab = lanternPrefab,
                    Lod0Mesh = isGlowingProp ? glowingLanternLod0 : rockLod0,
                    Lod1Mesh = rockLod0,
                    Lod2Mesh = rockLod0,
                    FoundationRadiusMeters = 0.75f,
                    AutoLevelTerrainOnPlace = false,
                    CanWeldIntoStaticAssembly = true,
                    HasBakedVertexEmissiveGlow = isGlowingProp
                };
                index++;
            }

            // 5. 15 Live Media & Pet Props (IDs 218..232)
            for (int m = 0; m < 15; m++)
            {
                db.Items[index] = new ArchitecturalCatalogItem
                {
                    DisplayName = ArchitecturalCatalogDatabase.All15LiveMediaAndPetPropNames[m],
                    CatalogId = index,
                    Category = ArchitecturalCategory.LiveMediaOrPetProp,
                    Theme = ArchitecturalTheme.UniversalPropOrGazebo,
                    Prefab = lanternPrefab,
                    Lod0Mesh = mediaPropLod0,
                    Lod1Mesh = mediaPropLod0,
                    Lod2Mesh = mediaPropLod0,
                    FoundationRadiusMeters = 0.6f,
                    AutoLevelTerrainOnPlace = false,
                    // Live Media Screens and interactive Pet Fetch Balls stay unwelded so they remain interactive!
                    CanWeldIntoStaticAssembly = false,
                    UsesOpaqueMatCapGlass = (m >= 2 && m <= 4)
                };
                index++;
            }
        }

        private static GameObject CreateReusableArchPrefab(string prefabName, Mesh mesh, Material mat, bool canWeld)
        {
            string path = $"{PrefabsFolder}/{prefabName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject temp = new GameObject(prefabName);
            MeshFilter mf = temp.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = temp.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            BoxCollider col = temp.AddComponent<BoxCollider>();
            col.size = mesh.bounds.size;
            col.center = mesh.bounds.center;

            MagneticSocketSnapper snapper = temp.AddComponent<MagneticSocketSnapper>();
            snapper.CanBeWelded = canWeld;

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return savedPrefab;
        }
    }
}
#endif
