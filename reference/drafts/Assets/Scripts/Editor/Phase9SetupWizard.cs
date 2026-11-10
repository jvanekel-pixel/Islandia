#if UNITY_EDITOR
using IslandBuilder.Audio;
using UnityEditor;
using UnityEngine;

namespace IslandBuilder.EditorTools
{
    public static class Phase9SetupWizard
    {
        private const string Phase9Folder = "Assets/Generated/Phase9";
        private const string VuMatPath = "Assets/Generated/Phase9/M_BoxRadioVuMeter.mat";
        private const string PrefabsFolder = "Assets/Generated/Phase9/Prefabs";

        [MenuItem("IslandBuilder/Phase 9/1. Generate Spatial Box Radio, Internet/User Audio Engine & VU Shader", false, 70)]
        public static void SetupPhase9SpatialAudioSuite()
        {
            EnsureFolders();

            Shader vuShader = Shader.Find("IslandBuilder/BoxRadioVuMeter");
            if (vuShader == null)
            {
                Debug.LogError("[Phase 9 Wizard] Could not find BoxRadioVuMeter.shader!");
                return;
            }

            Material vuMat = AssetDatabase.LoadAssetAtPath<Material>(VuMatPath);
            if (vuMat == null)
            {
                vuMat = new Material(vuShader);
                AssetDatabase.CreateAsset(vuMat, VuMatPath);
            }
            vuMat.shader = vuShader;
            vuMat.enableInstancing = true;
            EditorUtility.SetDirty(vuMat);

            // 1. Create Phase 9 System Root in Scene
            GameObject audioRoot = GameObject.Find("_Phase9_SpatialAudio_System");
            if (audioRoot == null) audioRoot = new GameObject("_Phase9_SpatialAudio_System");

            if (audioRoot.GetComponent<PrimaryAudioCatalogAndScanner>() == null)
                audioRoot.AddComponent<PrimaryAudioCatalogAndScanner>();

            if (audioRoot.GetComponent<SpatialRadioNetworkManager>() == null)
                audioRoot.AddComponent<SpatialRadioNetworkManager>();

            // 2. Generate Placeable 3D Spatial Box Radio & Retro Boombox Prefabs
            GameObject boxRadioPrefab = CreateSpatialRadioPrefab(
                "Prefab_Spatial_Box_Radio",
                new Vector3(0.46f, 0.28f, 0.18f),
                vuMat);

            CreateSpatialRadioPrefab(
                "Prefab_Retro_80s_Boombox",
                new Vector3(0.62f, 0.32f, 0.20f),
                vuMat);

            // Spawn one starter Box Radio near the center of the island so user hears spatial audio immediately on Play
            if (Object.FindFirstObjectByType<SpatialBoxRadioController>() == null && boxRadioPrefab != null)
            {
                GameObject sceneRadio = (GameObject)PrefabUtility.InstantiatePrefab(boxRadioPrefab);
                sceneRadio.name = "Starter_Island_Box_Radio";
                sceneRadio.transform.position = new Vector3(1.5f, 0.0f, 2.5f);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 9 Wizard] Generated Spatial Box Radio & Boombox Prefabs, P1 #7 Primary Internet/User Audio Catalog, and VU Meter Shader!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Phase9Folder))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase9");
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
                AssetDatabase.CreateFolder(Phase9Folder, "Prefabs");
        }

        private static GameObject CreateSpatialRadioPrefab(string prefabName, Vector3 bodySize, Material vuMat)
        {
            string fullPath = $"{PrefabsFolder}/{prefabName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
            if (existing != null) return existing;

            GameObject root = new GameObject(prefabName);

            // 1. Acoustic Cabinet Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Radio_Cabinet_Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, bodySize.y * 0.5f, 0f);
            body.transform.localScale = bodySize;

            // 2. Glowing Frequency Tuner & 16-Bar LED VU Meter Faceplate
            GameObject faceplate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            faceplate.name = "Radio_VU_Tuner_Faceplate";
            Object.DestroyImmediate(faceplate.GetComponent<Collider>());
            faceplate.transform.SetParent(root.transform, false);
            faceplate.transform.localPosition = new Vector3(0f, bodySize.y * 0.58f, -bodySize.z * 0.505f);
            faceplate.transform.localScale = new Vector3(bodySize.x * 0.65f, bodySize.y * 0.48f, 1.0f);
            MeshRenderer fpRenderer = faceplate.GetComponent<MeshRenderer>();
            fpRenderer.sharedMaterial = vuMat;

            // 3. Rotary Volume Knob (Left) & Station Tuning Knob (Right)
            Transform volKnob = CreateRotaryKnob(root.transform, "Knob_Volume_Rotary",
                new Vector3(-bodySize.x * 0.38f, bodySize.y * 0.72f, -bodySize.z * 0.52f));
            Transform tuneKnob = CreateRotaryKnob(root.transform, "Knob_Tuning_Rotary",
                new Vector3(bodySize.x * 0.38f, bodySize.y * 0.72f, -bodySize.z * 0.52f));
            Transform pwrBtn = CreateRotaryKnob(root.transform, "Button_Power_Toggle",
                new Vector3(0f, bodySize.y * 0.92f, 0f));

            var controller = root.AddComponent<SpatialBoxRadioController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("volumeKnobTransform").objectReferenceValue = volKnob;
            so.FindProperty("stationTuningKnobTransform").objectReferenceValue = tuneKnob;
            so.FindProperty("powerButtonTransform").objectReferenceValue = pwrBtn;
            so.FindProperty("vuMeterFaceplateRenderer").objectReferenceValue = fpRenderer;
            so.ApplyModifiedProperties();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, fullPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static Transform CreateRotaryKnob(Transform parent, string name, Vector3 localPos)
        {
            GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knob.name = name;
            knob.transform.SetParent(parent, false);
            knob.transform.localPosition = localPos;
            knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            knob.transform.localScale = new Vector3(0.055f, 0.02f, 0.055f);
            return knob.transform;
        }
    }
}
#endif
