#if UNITY_EDITOR
using IslandBuilder.UX;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace IslandBuilder.EditorTools
{
    public static class Phase3SetupWizard
    {
        private const string Phase3Folder = "Assets/Generated/Phase3";
        private const string UiMatPath = "Assets/Generated/Phase3/M_AppSWCompliantWorldUI.mat";

        [MenuItem("IslandBuilder/Phase 3/1. Wire Dual-Scale UX, Studio Gizmos & Wrist HUD", false, 20)]
        public static void SetupPhase3UxSuite()
        {
            EnsureFolders();

            Shader uiShader = Shader.Find("IslandBuilder/AppSWCompliantWorldUI");
            if (uiShader == null)
            {
                Debug.LogError("[Phase 3 Wizard] Could not find AppSWCompliantWorldUI.shader.");
                return;
            }

            Material uiMat = AssetDatabase.LoadAssetAtPath<Material>(UiMatPath);
            if (uiMat == null)
            {
                uiMat = new Material(uiShader);
                AssetDatabase.CreateAsset(uiMat, UiMatPath);
            }

            // 1. Ensure UndoRedoCommandStack & DualScaleWorldRigController exist
            GameObject uxRoot = GameObject.Find("_Phase3_UX_System");
            if (uxRoot == null) uxRoot = new GameObject("_Phase3_UX_System");

            if (uxRoot.GetComponent<UndoRedoCommandStack>() == null)
                uxRoot.AddComponent<UndoRedoCommandStack>();

            if (uxRoot.GetComponent<DualScaleWorldRigController>() == null)
                uxRoot.AddComponent<DualScaleWorldRigController>();

            var toybox = uxRoot.GetComponent<ToyboxInteractionController>();
            if (toybox == null) toybox = uxRoot.AddComponent<ToyboxInteractionController>();

            var studio = uxRoot.GetComponent<ArtistStudioGridGizmoController>();
            if (studio == null) studio = uxRoot.AddComponent<ArtistStudioGridGizmoController>();

            // 2. Create 3-Axis Studio Gizmo Visual
            Transform gizmoRoot = CreateThreeAxisGizmo(uxRoot.transform);
            SerializedObject studioSo = new SerializedObject(studio);
            studioSo.FindProperty("visualGizmoRoot").objectReferenceValue = gizmoRoot;
            studioSo.ApplyModifiedProperties();

            // 3. Create Wrist Compositor HUD & Budget Gauge
            SetupWristCompositorHud(uxRoot, uiMat, toybox, studio);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 3 Wizard] Successfully wired Dual-Scale Rig, Toybox/Studio Controllers, Undo/Redo Stack, and Wrist Budget HUD!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Phase3Folder))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase3");
        }

        private static Transform CreateThreeAxisGizmo(Transform parent)
        {
            Transform existing = parent.Find("Studio_3Axis_Gizmo");
            if (existing != null) return existing;

            GameObject root = new GameObject("Studio_3Axis_Gizmo");
            root.transform.SetParent(parent, false);

            CreateAxisHandle(root.transform, "Handle_AxisX", new Vector3(0.6f, 0f, 0f), new Vector3(1.2f, 0.08f, 0.08f));
            CreateAxisHandle(root.transform, "Handle_AxisY", new Vector3(0f, 0.6f, 0f), new Vector3(0.08f, 1.2f, 0.08f));
            CreateAxisHandle(root.transform, "Handle_AxisZ", new Vector3(0f, 0f, 0.6f), new Vector3(0.08f, 0.08f, 1.2f));

            root.SetActive(false);
            return root.transform;
        }

        private static void CreateAxisHandle(Transform parent, string name, Vector3 localPos, Vector3 scale)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = name;
            handle.transform.SetParent(parent, false);
            handle.transform.localPosition = localPos;
            handle.transform.localScale = scale;
        }

        private static void SetupWristCompositorHud(
            GameObject uxRoot,
            Material uiMat,
            ToyboxInteractionController toybox,
            ArtistStudioGridGizmoController studio)
        {
            var hud = uxRoot.GetComponent<WristCompositorHudController>();
            if (hud == null) hud = uxRoot.AddComponent<WristCompositorHudController>();

            GameObject quadGo = GameObject.Find("Wrist_Compositor_Quad");
            if (quadGo == null)
            {
                quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadGo.name = "Wrist_Compositor_Quad";
                Object.DestroyImmediate(quadGo.GetComponent<Collider>());
                quadGo.transform.SetParent(uxRoot.transform, false);
                quadGo.transform.localScale = new Vector3(0.18f, 0.135f, 1.0f);
                quadGo.GetComponent<MeshRenderer>().sharedMaterial = uiMat;
            }

            SerializedObject hudSo = new SerializedObject(hud);
            hudSo.FindProperty("wristHudVisualRoot").objectReferenceValue = quadGo;
            hudSo.FindProperty("compositorFallbackQuadRenderer").objectReferenceValue = quadGo.GetComponent<MeshRenderer>();
            hudSo.FindProperty("toyboxController").objectReferenceValue = toybox;
            hudSo.FindProperty("studioController").objectReferenceValue = studio;
            hudSo.ApplyModifiedProperties();
        }
    }
}
#endif
