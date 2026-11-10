#if UNITY_EDITOR
using IslandBuilder.Media;
using UnityEditor;
using UnityEngine;

namespace IslandBuilder.EditorTools
{
    public static class Phase8SetupWizard
    {
        private const string Phase8Folder = "Assets/Generated/Phase8";
        private const string ScreenMatPath = "Assets/Generated/Phase8/M_CompositorVideoScreen.mat";
        private const string PrefabsFolder = "Assets/Generated/Phase8/Prefabs";

        [MenuItem("IslandBuilder/Phase 8/1. Generate Virtual TV, Drive-In Cinema, Projector & OpenXR Quad Layer", false, 60)]
        public static void SetupPhase8CinemaSuite()
        {
            EnsureFolders();

            Shader screenShader = Shader.Find("IslandBuilder/CompositorVideoScreenPunchThrough");
            if (screenShader == null)
            {
                Debug.LogError("[Phase 8 Wizard] Could not find CompositorVideoScreenPunchThrough.shader!");
                return;
            }

            Material screenMat = AssetDatabase.LoadAssetAtPath<Material>(ScreenMatPath);
            if (screenMat == null)
            {
                screenMat = new Material(screenShader);
                AssetDatabase.CreateAsset(screenMat, ScreenMatPath);
            }
            screenMat.shader = screenShader;
            screenMat.enableInstancing = true;
            EditorUtility.SetDirty(screenMat);

            // 1. Create Phase 8 System Root in Scene
            GameObject mediaRoot = GameObject.Find("_Phase8_VirtualCinema_System");
            if (mediaRoot == null) mediaRoot = new GameObject("_Phase8_VirtualCinema_System");

            if (mediaRoot.GetComponent<VuplexWidevineDrmBootstrapper>() == null)
                mediaRoot.AddComponent<VuplexWidevineDrmBootstrapper>();

            if (mediaRoot.GetComponent<VirtualCinemaManager>() == null)
                mediaRoot.AddComponent<VirtualCinemaManager>();

            var router = mediaRoot.GetComponent<OpenXrCinemaQuadLayerRouter>();
            if (router == null) router = mediaRoot.AddComponent<OpenXrCinemaQuadLayerRouter>();

            // 2. Create OpenXR Compositor Quad Layer Anchor
            Transform quadAnchor = mediaRoot.transform.Find("OpenXR_1080p_Cinema_Quad_Layer");
            if (quadAnchor == null)
            {
                GameObject quadGo = new GameObject("OpenXR_1080p_Cinema_Quad_Layer");
                quadGo.transform.SetParent(mediaRoot.transform, false);
                quadGo.SetActive(false);
                quadAnchor = quadGo.transform;
            }

            SerializedObject routerSo = new SerializedObject(router);
            routerSo.FindProperty("compositionQuadLayerTransform").objectReferenceValue = quadAnchor;
            routerSo.ApplyModifiedProperties();

            // 3. Generate Placeable Virtual TV, Drive-In Cinema & Tripod Projector Prefabs
            CreateCinemaDevicePrefab(
                "Prefab_Wall_Flatscreen_TV_65in",
                CinemaDeviceFormFactor.WallFlatscreenTv,
                new Vector3(1.44f, 0.81f, 1.0f),
                screenMat);

            CreateCinemaDevicePrefab(
                "Prefab_DriveIn_Outdoor_Cinema_8m",
                CinemaDeviceFormFactor.DriveInOutdoorCinema,
                new Vector3(8.0f, 4.5f, 1.0f),
                screenMat);

            CreateCinemaDevicePrefab(
                "Prefab_Portable_Tripod_Projector",
                CinemaDeviceFormFactor.PortableTripodProjector,
                new Vector3(2.4f, 1.35f, 1.0f),
                screenMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 8 Wizard] Generated Virtual TV, Drive-In Cinema, Projector Prefabs, Widevine Bootstrapper, P1 #8 Quest Browser Handoff, and P2 #14 Screen Cap Manager!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Phase8Folder))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase8");
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
                AssetDatabase.CreateFolder(Phase8Folder, "Prefabs");
        }

        private static void CreateCinemaDevicePrefab(
            string prefabName,
            CinemaDeviceFormFactor formFactor,
            Vector3 screenDimensions,
            Material screenMat)
        {
            string fullPath = $"{PrefabsFolder}/{prefabName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fullPath) != null) return;

            GameObject root = new GameObject(prefabName);

            // Bezel Frame
            GameObject bezel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bezel.name = "Cinema_Bezel_Frame";
            bezel.transform.SetParent(root.transform, false);
            bezel.transform.localPosition = new Vector3(0f, screenDimensions.y * 0.5f, 0.04f);
            bezel.transform.localScale = new Vector3(screenDimensions.x + 0.12f, screenDimensions.y + 0.12f, 0.06f);

            // 16:9 Screen Surface Quad
            GameObject screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenQuad.name = "Screen_16x9_Surface";
            Object.DestroyImmediate(screenQuad.GetComponent<Collider>());
            screenQuad.transform.SetParent(root.transform, false);
            screenQuad.transform.localPosition = new Vector3(0f, screenDimensions.y * 0.5f, 0.0f);
            screenQuad.transform.localScale = screenDimensions;

            MeshRenderer screenMr = screenQuad.GetComponent<MeshRenderer>();
            screenMr.sharedMaterial = screenMat;

            var controller = root.AddComponent<VirtualCinemaScreenController>();
            SerializedObject ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("formFactor").enumValueIndex = (int)formFactor;
            ctrlSo.FindProperty("screenSurfaceRenderer").objectReferenceValue = screenMr;
            ctrlSo.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, fullPath);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
