using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Island.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Island.Editor
{
    /// <summary>
    /// Generates the one boot scene (ADR-06): a <see cref="CompositionRoot"/> and the rig, and
    /// nothing else. Everything the player sees is built by the root, in code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rerunning it is a no-op.</b> Unity gives every object in a newly made scene fresh file
    /// IDs, so regenerating a scene that was already right would rewrite the file for nothing and
    /// leave a diff behind. Instead the scene on disk is described — names, components, the
    /// camera's settings — and compared with <see cref="Expected"/>; it is only rebuilt when the
    /// two differ. The same comparison is the test that the committed scene is the right one.
    /// </para>
    /// <para>
    /// It also makes the boot scene the first, and only, scene in the build.
    /// </para>
    /// <para>
    /// The rig is plain on purpose: an origin, an offset, and a camera. The head pose comes from
    /// <see cref="IInputSource"/> through the root, the same way on the headset and in the
    /// simulation, so there is no tracked-pose component here to behave differently in each.
    /// </para>
    /// </remarks>
    public static class BootSceneBuilder
    {
        public const string ScenesFolder = "Assets/Island/Generated/Scenes";
        public const string BootScenePath = ScenesFolder + "/Boot.unity";

        public const string RootName = "CompositionRoot";
        public const string RigName = "Rig";
        public const string OffsetName = "Camera Offset";
        public const string CameraName = "Main Camera";

        /// <summary>Close enough to see a hand held at the face; ADR-03 leaves the rest to culling.</summary>
        public const float NearClip = 0.05f;

        /// <summary>Beyond the island's diagonal and its neighbours on the horizon.</summary>
        public const float FarClip = 500f;

        /// <summary>The scene this builder makes, as <see cref="Describe(Scene)"/> writes it.</summary>
        public static readonly string Expected = string.Join("\n", new[]
        {
            $"{RootName} [{typeof(CompositionRoot).FullName}]",
            $"{RigName} [{typeof(SceneRig).FullName}]",
            $"  {OffsetName} []",
            $"    {CameraName} [{typeof(Camera).FullName}, {typeof(AudioListener).FullName}, {typeof(UniversalAdditionalCameraData).FullName}] tag=MainCamera near={Number(NearClip)} far={Number(FarClip)}",
        });

        /// <summary>
        /// Makes the boot scene and registers it, unless both are already right.
        /// </summary>
        /// <returns>True if anything was written.</returns>
        [MenuItem("Island/Build boot scene")]
        public static bool Build()
        {
            try
            {
                var wrote = false;

                if (DescribeOnDisk() != Expected)
                {
                    Generate();
                    wrote = true;
                    var actual = DescribeOnDisk();
                    if (actual != Expected)
                        throw new InvalidOperationException(
                            $"The boot scene did not come out as specified.\nExpected:\n{Expected}\nGot:\n{actual}");
                }

                wrote |= RegisterInBuild();

                Debug.Log(wrote
                    ? $"[Island] Boot scene written to {BootScenePath}."
                    : "[Island] Boot scene already up to date; nothing written.");
                return wrote;
            }
            catch (Exception error)
            {
                // Rethrown so -executeMethod exits non-zero (ADR-06).
                Debug.LogError($"[Island] Building the boot scene failed: {error}");
                throw;
            }
        }

        /// <summary>For <c>-executeMethod</c>.</summary>
        public static void BuildAndExit()
        {
            try
            {
                Build();
            }
            catch (Exception)
            {
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// The scene at <see cref="BootScenePath"/>, described; or null if there is none. Opens it
        /// alongside whatever is loaded and closes it again, changing nothing.
        /// </summary>
        public static string DescribeOnDisk()
        {
            if (!File.Exists(BootScenePath)) return null;

            var already = SceneManager.GetSceneByPath(BootScenePath);
            if (already.IsValid() && already.isLoaded) return Describe(already);

            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Additive);
            try
            {
                return Describe(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        /// <summary>Every object in the scene, its components and the settings that matter.</summary>
        public static string Describe(Scene scene)
        {
            var lines = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
                DescribeObject(root.transform, 0, lines);
            return string.Join("\n", lines);
        }

        private static void DescribeObject(Transform transform, int depth, List<string> lines)
        {
            var components = transform.GetComponents<Component>()
                .Where(component => !(component is Transform))
                .Select(component => component == null ? "<missing script>" : component.GetType().FullName);

            var line = new StringBuilder()
                .Append(' ', depth * 2)
                .Append(transform.name)
                .Append(" [").Append(string.Join(", ", components)).Append(']');

            if (!transform.CompareTag("Untagged")) line.Append(" tag=").Append(transform.tag);

            var camera = transform.GetComponent<Camera>();
            if (camera != null)
                line.Append(" near=").Append(Number(camera.nearClipPlane))
                    .Append(" far=").Append(Number(camera.farClipPlane));

            // A rig or camera moved off the origin in the scene would offset every pose the input
            // supplies, so a non-identity transform is part of the description.
            if (transform.localPosition != Vector3.zero || transform.localRotation != Quaternion.identity
                || transform.localScale != Vector3.one)
                line.Append(" moved");

            lines.Add(line.ToString());
            for (var i = 0; i < transform.childCount; i++)
                DescribeObject(transform.GetChild(i), depth + 1, lines);
        }

        private static string Number(float value) =>
            value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        private static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets/Island/Generated", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject(RootName, typeof(CompositionRoot));

            var rig = new GameObject(RigName, typeof(SceneRig));
            var offset = new GameObject(OffsetName);
            offset.transform.SetParent(rig.transform, false);

            var head = new GameObject(CameraName, typeof(Camera), typeof(AudioListener));
            head.transform.SetParent(offset.transform, false);
            head.tag = "MainCamera";
            var camera = head.GetComponent<Camera>();
            camera.nearClipPlane = NearClip;
            camera.farClipPlane = FarClip;
            // Added here rather than left for URP to add on first render, so the scene on disk is
            // the scene that runs.
            camera.GetUniversalAdditionalCameraData();

            if (!EditorSceneManager.SaveScene(scene, BootScenePath))
                throw new IOException($"Unity could not save {BootScenePath}.");
            AssetDatabase.SaveAssets();
        }

        /// <summary>Makes the boot scene the build's only scene. True if that changed anything.</summary>
        private static bool RegisterInBuild()
        {
            var current = EditorBuildSettings.scenes;
            if (current.Length == 1 && current[0].enabled && current[0].path == BootScenePath)
                return false;

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
