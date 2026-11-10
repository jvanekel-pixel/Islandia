#if UNITY_EDITOR
using IslandBuilder.AI;
using IslandBuilder.UX;
using UnityEditor;
using UnityEngine;

namespace IslandBuilder.EditorTools
{
    public static class Phase11SetupWizard
    {
        private const string Phase11Folder = "Assets/Generated/Phase11";
        private const string FetchBallPrefabPath = "Assets/Generated/Phase11/Prefab_Island_Fetch_Ball.prefab";

        [MenuItem("IslandBuilder/Phase 11/1. Wire 4Hz Pet Harmony AI, Chunked NavMesh & Comfort/Hands", false, 90)]
        public static void SetupPhase11PetHarmonyAndNavMesh()
        {
            EnsureFolders();

            // 1. Create or locate the Phase 10/11 Pet Sanctuary & AI System Root
            GameObject aiRoot = GameObject.Find("_Phase10_11_PetSanctuary_System");
            if (aiRoot == null) aiRoot = new GameObject("_Phase10_11_PetSanctuary_System");

            if (aiRoot.GetComponent<ChunkedDynamicNavMeshManager>() == null)
                aiRoot.AddComponent<ChunkedDynamicNavMeshManager>();

            var coordinator = aiRoot.GetComponent<PetHarmonyCoordinator>();
            if (coordinator == null) coordinator = aiRoot.AddComponent<PetHarmonyCoordinator>();

            // 2. Create Interactive Fetch Ball Prefab & Scene Instance
            GameObject fetchBallPrefab = CreateFetchBallPrefab();
            PetFetchableToy sceneFetchBall = Object.FindFirstObjectByType<PetFetchableToy>();
            if (sceneFetchBall == null && fetchBallPrefab != null)
            {
                GameObject ballGo = (GameObject)PrefabUtility.InstantiatePrefab(fetchBallPrefab);
                ballGo.name = "Island_Starter_Fetch_Ball";
                ballGo.transform.position = new Vector3(0f, 0.5f, 2.0f);
                sceneFetchBall = ballGo.GetComponent<PetFetchableToy>();
            }

            SerializedObject coordSo = new SerializedObject(coordinator);
            coordSo.FindProperty("defaultIslandFetchBall").objectReferenceValue = sceneFetchBall;
            coordSo.ApplyModifiedProperties();

            // 3. Wire P2 #12 & #13 VR Comfort Locomotion & Hand Tracking Manager
            GameObject uxRoot = GameObject.Find("_Phase3_UX_System");
            if (uxRoot == null) uxRoot = new GameObject("_Phase3_UX_System");

            if (uxRoot.GetComponent<VRComfortAndHandTrackingManager>() == null)
                uxRoot.AddComponent<VRComfortAndHandTrackingManager>();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 11 Wizard] Wired ChunkedDynamicNavMeshManager, 4Hz PetHarmonyCoordinator (<0.5ms AI Gate), Interactive Fetch Ball, and P2 #12/#13 VR Comfort & Hand Tracking!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Phase11Folder))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase11");
        }

        private static GameObject CreateFetchBallPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(FetchBallPrefabPath);
            if (existing != null) return existing;

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Prefab_Island_Fetch_Ball";
            ball.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);

            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.25f;

            ball.AddComponent<PetFetchableToy>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(ball, FetchBallPrefabPath);
            Object.DestroyImmediate(ball);
            return saved;
        }
    }
}
#endif
