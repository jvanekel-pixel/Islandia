#if UNITY_EDITOR
using System.Collections.Generic;
using IslandBuilder.Pets;
using UnityEditor;
using UnityEngine;

namespace IslandBuilder.EditorTools
{
    public static class Phase10SetupWizard
    {
        private const string Phase10Folder = "Assets/Generated/Phase10";
        private const string CoatMatPath = "Assets/Generated/Phase10/M_PetCoatPatternLut.mat";
        private const string MeshesPath = "Assets/Generated/Phase10/Phase10_RiggedPetMeshes.asset";
        private const string DatabasePath = "Assets/Generated/Phase10/PetArchetypeDatabase.asset";
        private const string PrefabsFolder = "Assets/Generated/Phase10/Prefabs";

        [MenuItem("IslandBuilder/Phase 10/1. Generate 7 Rigged Pet Archetypes, BlendShapes & Sanctuary Studio", false, 80)]
        public static void GeneratePhase10PetSanctuarySuite()
        {
            EnsureFolders();

            // Enforce Vulkan Compute Skinning in PlayerSettings
            PlayerSettings.gpuSkinning = true;

            Shader coatShader = Shader.Find("IslandBuilder/PetCoatPatternLut");
            if (coatShader == null)
            {
                Debug.LogError("[Phase 10 Wizard] Could not find PetCoatPatternLut.shader!");
                return;
            }

            Material coatMat = AssetDatabase.LoadAssetAtPath<Material>(CoatMatPath);
            if (coatMat == null)
            {
                coatMat = new Material(coatShader);
                AssetDatabase.CreateAsset(coatMat, CoatMatPath);
            }
            coatMat.shader = coatShader;
            coatMat.enableInstancing = true;
            EditorUtility.SetDirty(coatMat);

            PetArchetypeDatabase db = AssetDatabase.LoadAssetAtPath<PetArchetypeDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<PetArchetypeDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            db.SharedPetCoatMaterial = coatMat;
            db.Archetypes = new PetArchetypeDefinition[PetArchetypeDatabase.TotalArchetypeCount];

            // Default vibrant coat palettes for all 7 species
            (Color prim, Color sec, Color acc, CoatPatternStyle pat, Vector3 bodyScale, string defaultName)[] speciesPresets =
            {
                (new Color(0.86f, 0.62f, 0.25f), new Color(0.96f, 0.92f, 0.82f), new Color(0.32f, 0.20f, 0.12f), CoatPatternStyle.SpottedOrCalico,   new Vector3(1.00f, 1.00f, 1.00f), "Barnaby"),
                (new Color(0.92f, 0.88f, 0.82f), new Color(0.98f, 0.98f, 0.96f), new Color(0.85f, 0.42f, 0.18f), CoatPatternStyle.SpottedOrCalico,   new Vector3(0.82f, 0.82f, 0.85f), "Mochi"),
                (new Color(0.90f, 0.36f, 0.12f), new Color(0.98f, 0.95f, 0.90f), new Color(0.14f, 0.12f, 0.12f), CoatPatternStyle.SolidAndSocks,     new Vector3(0.90f, 0.88f, 0.95f), "Kitsune"),
                (new Color(0.58f, 0.40f, 0.24f), new Color(0.68f, 0.52f, 0.36f), new Color(0.32f, 0.22f, 0.15f), CoatPatternStyle.SolidAndSocks,     new Vector3(1.15f, 0.92f, 1.10f), "Yuzu"),
                (new Color(0.76f, 0.24f, 0.12f), new Color(0.16f, 0.14f, 0.14f), new Color(0.96f, 0.92f, 0.86f), CoatPatternStyle.TigerOrRingStripe, new Vector3(0.88f, 0.85f, 0.95f), "Bamboozle"),
                (new Color(0.16f, 0.72f, 0.52f), new Color(0.88f, 0.78f, 0.32f), new Color(0.10f, 0.32f, 0.65f), CoatPatternStyle.SpottedOrCalico,   new Vector3(0.95f, 1.05f, 1.05f), "Ignis"),
                (new Color(0.92f, 0.14f, 0.18f), new Color(0.98f, 0.82f, 0.12f), new Color(0.12f, 0.38f, 0.92f), CoatPatternStyle.TigerOrRingStripe, new Vector3(0.75f, 0.95f, 0.80f), "Rio")
            };

            Mesh firstMeshAsset = null;

            for (int i = 0; i < PetArchetypeDatabase.TotalArchetypeCount; i++)
            {
                PetSpeciesArchetype species = (PetSpeciesArchetype)i;
                string speciesName = PetArchetypeDatabase.All7SpeciesNames[i];
                var preset = speciesPresets[i];

                Mesh riggedMesh = BuildRiggedPetMeshWith4BlendShapes($"Mesh_Pet_{species}", preset.bodyScale);
                if (i == 0)
                {
                    firstMeshAsset = riggedMesh;
                    AssetDatabase.CreateAsset(firstMeshAsset, MeshesPath);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(riggedMesh, MeshesPath);
                }

                PetCustomizationProfile defaultProf = new PetCustomizationProfile
                {
                    Archetype = species,
                    CustomPetName = preset.defaultName,
                    PrimaryCoatColor = preset.prim,
                    SecondaryBellyColor = preset.sec,
                    AccentSpotSockColor = preset.acc,
                    PatternStyle = preset.pat,
                    PatternFrequency = 7.5f,
                    EarDroopOrCrestWeight = 20f,
                    SnoutLengthWeight = 35f,
                    TailFluffOrWingWeight = 50f,
                    BodyChubbinessWeight = i == (int)PetSpeciesArchetype.ChillCapybara ? 85f : 25f
                };

                GameObject prefab = CreateRiggedPetPrefab($"Prefab_Pet_{species}", riggedMesh, coatMat, defaultProf);

                db.Archetypes[i] = new PetArchetypeDefinition
                {
                    DisplaySpeciesName = speciesName,
                    Archetype = species,
                    RiggedPrefab = prefab,
                    DefaultWalkSpeedMetersPerSec = 1.6f,
                    DefaultRunSpeedMetersPerSec = 3.4f,
                    DefaultAppearance = defaultProf
                };
            }

            EditorUtility.SetDirty(db);

            // Wire IslandPetSanctuaryManager in active scene
            GameObject petSysGo = GameObject.Find("_Phase10_11_PetSanctuary_System");
            if (petSysGo == null) petSysGo = new GameObject("_Phase10_11_PetSanctuary_System");

            var sanctuary = petSysGo.GetComponent<IslandPetSanctuaryManager>();
            if (sanctuary == null) sanctuary = petSysGo.AddComponent<IslandPetSanctuaryManager>();

            SerializedObject so = new SerializedObject(sanctuary);
            so.FindProperty("archetypeDatabase").objectReferenceValue = db;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 10 Wizard] Generated 7 Rigged Pet Archetypes (<=20 Bones, 4 BlendShapes, RGBA Coat Masks, Baked Collar Tags) & wired IslandPetSanctuaryManager!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Phase10Folder))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase10");
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
                AssetDatabase.CreateFolder(Phase10Folder, "Prefabs");
        }

        /// <summary>
        /// Builds a rigged quadruped/avian mesh weighted to 7 bones:
        /// 0: RootSpine, 1: Head, 2: Tail, 3: FrontLeftLeg, 4: FrontRightLeg, 5: BackLeftLeg, 6: BackRightLeg
        /// plus 4 procedural BlendShape frames (EarDroop, SnoutLength, TailFluff, BodyChubbiness).
        /// </summary>
        private static Mesh BuildRiggedPetMeshWith4BlendShapes(string meshName, Vector3 scaleMod)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> cols = new List<Color>();
            List<BoneWeight> weights = new List<BoneWeight>();
            List<int> tris = new List<int>();

            // 0: Torso + Collar Ring (Bone 0 = RootSpine)
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(0f, 0.28f, 0f),
                size: Vector3.Scale(new Vector3(0.28f, 0.24f, 0.52f), scaleMod),
                boneIndex: 0,
                vertexMask: new Color(0.25f, 0.90f, 0.0f, 0.85f)); // G=0.9 allows spots/stripes on torso

            // Collar Band around neck (color.a = 0.98 triggers Collar Nameplate Texture in shader!)
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(0f, 0.34f, 0.22f * scaleMod.z),
                size: new Vector3(0.24f, 0.06f, 0.08f),
                boneIndex: 1,
                vertexMask: new Color(0f, 0f, 0f, 0.98f));

            // 1: Head, Snout & Ears (Bone 1 = Head)
            int headVertStart = verts.Count;
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(0f, 0.44f, 0.30f * scaleMod.z),
                size: new Vector3(0.24f, 0.22f, 0.26f),
                boneIndex: 1,
                vertexMask: new Color(0.35f, 0.50f, 0.20f, 0.90f));

            // Left & Right Ears (weighted to Head Bone 1, morphed by BlendShape 0)
            int earsVertStart = verts.Count;
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(-0.09f, 0.58f, 0.26f * scaleMod.z),
                size: new Vector3(0.06f, 0.14f, 0.06f),
                boneIndex: 1,
                vertexMask: new Color(0f, 0f, 0.95f, 0.90f)); // B=0.95 -> Ear Tips Accent Color!
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(0.09f, 0.58f, 0.26f * scaleMod.z),
                size: new Vector3(0.06f, 0.14f, 0.06f),
                boneIndex: 1,
                vertexMask: new Color(0f, 0f, 0.95f, 0.90f));
            int earsVertEnd = verts.Count;

            // 2: Fluffy Tail (Bone 2 = Tail, morphed by BlendShape 2)
            int tailVertStart = verts.Count;
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(0f, 0.36f, -0.34f * scaleMod.z),
                size: new Vector3(0.10f, 0.12f, 0.28f),
                boneIndex: 2,
                vertexMask: new Color(0.1f, 1.0f, 0.6f, 0.85f));
            int tailVertEnd = verts.Count;

            // 3..6: Four Legs with Paw Socks (color.b = 1.0 on lower paws!)
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                new Vector3(-0.10f, 0.12f, 0.18f), new Vector3(0.08f, 0.24f, 0.08f), boneIndex: 3, new Color(0f, 0f, 1.0f, 0.55f));
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                new Vector3(0.10f, 0.12f, 0.18f), new Vector3(0.08f, 0.24f, 0.08f), boneIndex: 4, new Color(0f, 0f, 1.0f, 0.55f));
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                new Vector3(-0.10f, 0.12f, -0.18f), new Vector3(0.08f, 0.24f, 0.08f), boneIndex: 5, new Color(0f, 0f, 1.0f, 0.55f));
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                new Vector3(0.10f, 0.12f, -0.18f), new Vector3(0.08f, 0.24f, 0.08f), boneIndex: 6, new Color(0f, 0f, 1.0f, 0.55f));

            Mesh m = new Mesh { name = meshName };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetColors(cols);
            m.boneWeights = weights.ToArray();
            m.SetTriangles(tris, 0);

            int vCount = verts.Count;
            Vector3[] zeroNormals = new Vector3[vCount];

            // BlendShape 0: Ear Droop / Crest Height
            Vector3[] deltaEars = new Vector3[vCount];
            for (int i = earsVertStart; i < earsVertEnd; i++)
            {
                deltaEars[i] = new Vector3(verts[i].x * 0.45f, -0.07f, 0.03f);
            }
            m.AddBlendShapeFrame("EarDroopOrCrest", 100f, deltaEars, zeroNormals, zeroNormals);

            // BlendShape 1: Snout Length
            Vector3[] deltaSnout = new Vector3[vCount];
            for (int i = headVertStart; i < earsVertStart; i++)
            {
                if (verts[i].z > 0.32f) deltaSnout[i] = new Vector3(0f, -0.02f, 0.12f);
            }
            m.AddBlendShapeFrame("SnoutLength", 100f, deltaSnout, zeroNormals, zeroNormals);

            // BlendShape 2: Tail Fluff / Plume Size
            Vector3[] deltaTail = new Vector3[vCount];
            for (int i = tailVertStart; i < tailVertEnd; i++)
            {
                deltaTail[i] = new Vector3(verts[i].x * 0.85f, 0.08f, -0.10f);
            }
            m.AddBlendShapeFrame("TailFluffOrWing", 100f, deltaTail, zeroNormals, zeroNormals);

            // BlendShape 3: Body Chubbiness (Chonk Slider)
            Vector3[] deltaChonk = new Vector3[vCount];
            for (int i = 0; i < headVertStart; i++)
            {
                deltaChonk[i] = new Vector3(verts[i].x * 0.45f, 0.03f, verts[i].z * 0.15f);
            }
            m.AddBlendShapeFrame("BodyChubbiness", 100f, deltaChonk, zeroNormals, zeroNormals);

            m.RecalculateBounds();
            return m;
        }

        private static void AppendSkinnedPart(
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<Color> cols,
            List<BoneWeight> weights, List<int> tris,
            Vector3 center, Vector3 size, int boneIndex, Color vertexMask)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cube = temp.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] cVerts = cube.vertices;
            Vector3[] cNorms = cube.normals;
            Vector2[] cUvs = cube.uv;
            int[] cTris = cube.triangles;
            Object.DestroyImmediate(temp);

            int baseIdx = verts.Count;
            BoneWeight bw = new BoneWeight { boneIndex0 = boneIndex, weight0 = 1.0f };

            for (int i = 0; i < cVerts.Length; i++)
            {
                Vector3 v = center + Vector3.Scale(cVerts[i], size);
                verts.Add(v);
                norms.Add(cNorms[i]);
                uvs.Add(cUvs[i]);

                // Underside vertices automatically receive Belly Mask (color.r = 1.0)
                Color mask = vertexMask;
                if (cNorms[i].y < -0.3f && mask.a < 0.92f)
                {
                    mask.r = 0.95f;
                }
                cols.Add(mask);
                weights.Add(bw);
            }

            for (int i = 0; i < cTris.Length; i++)
            {
                tris.Add(baseIdx + cTris[i]);
            }
        }

        private static GameObject CreateRiggedPetPrefab(
            string prefabName,
            Mesh riggedMesh,
            Material sharedCoatMat,
            PetCustomizationProfile defaultProfile)
        {
            string fullPath = $"{PrefabsFolder}/{prefabName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
            if (existing != null) return existing;

            GameObject root = new GameObject(prefabName);

            // Create 7-Bone Skeleton Hierarchy (< 20 Bones Mobile Gate)
            Vector3[] boneLocalPositions =
            {
                new Vector3(0f, 0.28f, 0f),     // 0: RootSpine
                new Vector3(0f, 0.16f, 0.30f),  // 1: Head (child of RootSpine)
                new Vector3(0f, 0.08f, -0.34f), // 2: Tail (child of RootSpine)
                new Vector3(-0.10f, -0.16f, 0.18f), // 3: FrontLeftLeg
                new Vector3(0.10f, -0.16f, 0.18f),  // 4: FrontRightLeg
                new Vector3(-0.10f, -0.16f, -0.18f),// 5: BackLeftLeg
                new Vector3(0.10f, -0.16f, -0.18f)  // 6: BackRightLeg
            };

            string[] boneNames =
            {
                "Bone_RootSpine", "Bone_Head", "Bone_Tail",
                "Bone_FrontLeftLeg", "Bone_FrontRightLeg", "Bone_BackLeftLeg", "Bone_BackRightLeg"
            };

            Transform[] bones = new Transform[7];
            Matrix4x4[] bindPoses = new Matrix4x4[7];

            for (int b = 0; b < 7; b++)
            {
                GameObject boneGo = new GameObject(boneNames[b]);
                boneGo.transform.SetParent(b == 0 ? root.transform : bones[0], false);
                boneGo.transform.localPosition = boneLocalPositions[b];
                bones[b] = boneGo.transform;
                bindPoses[b] = bones[b].worldToLocalMatrix * root.transform.localToWorldMatrix;
            }

            riggedMesh.bindposes = bindPoses;
            EditorUtility.SetDirty(riggedMesh);

            GameObject meshGo = new GameObject("Pet_SkinnedMesh");
            meshGo.transform.SetParent(root.transform, false);

            SkinnedMeshRenderer smr = meshGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = riggedMesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterial = sharedCoatMat;
            smr.updateWhenOffscreen = false; // Enforces CullUpdateTransforms off-screen savings
            smr.quality = SkinQuality.Bone2;

            // Lightweight capsule collider for VR petting & toy interaction
            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.28f, 0f);
            col.radius = 0.24f;
            col.height = 0.65f;
            col.direction = 2; // Z-axis length

            var controller = root.AddComponent<IslandPetController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("primarySkinnedRenderer").objectReferenceValue = smr;
            so.FindProperty("rootBone").objectReferenceValue = bones[0];
            so.FindProperty("headBone").objectReferenceValue = bones[1];
            so.FindProperty("tailBone").objectReferenceValue = bones[2];
            so.FindProperty("frontLeftLegBone").objectReferenceValue = bones[3];
            so.FindProperty("frontRightLegBone").objectReferenceValue = bones[4];
            so.FindProperty("backLeftLegBone").objectReferenceValue = bones[5];
            so.FindProperty("backRightLegBone").objectReferenceValue = bones[6];
            so.FindProperty("currentProfile").boxedValue = defaultProfile;
            so.ApplyModifiedProperties();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, fullPath);
            Object.DestroyImmediate(root);
            return saved;
        }
    }
}
#endif
