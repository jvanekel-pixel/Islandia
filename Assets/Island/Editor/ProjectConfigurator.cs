using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Island.Editor
{
    /// <summary>
    /// Sets every rendering decision in ADR-03, in code, and creates the assets they live on.
    /// The XR half is <see cref="XrConfigurator"/>, which <see cref="Apply"/> also runs (F-05).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ADR-06: editor generators are idempotent static methods callable with
    /// <c>-executeMethod</c>, they open and save what they touch, and they <b>throw on failure</b>
    /// so batchmode exits non-zero. A generator that fails quietly leaves a project that looks
    /// configured and is not — and the symptoms appear on a headset, weeks later.
    /// </para>
    /// <para>
    /// Every value here is from ADR-03 and nothing is a preference. Where a property's setter is
    /// <c>internal</c>, it is written through <see cref="SerializedObject"/> by its serialised field
    /// name rather than by reflection: reflection on a private field skips Unity's serialisation
    /// and the value is lost the next time the asset is written.
    /// </para>
    /// <para>
    /// <b>Render Graph is not set here.</b> In this editor version URP's
    /// <c>enableRenderCompatibilityMode</c> reads <c>false</c> unconditionally and its setter is
    /// marked <c>[Obsolete(..., error: true)]</c> — assigning it is a compile error. Render Graph is
    /// simply on, and <c>ProjectConfigurationTests</c> asserts that rather than this setting it.
    /// </para>
    /// </remarks>
    public static class ProjectConfigurator
    {
        /// <summary>Where the generated rendering assets live (ADR-06).</summary>
        public const string GeneratedFolder = "Assets/Island/Generated/Rendering";

        public const string PipelineAssetPath = GeneratedFolder + "/IslandPipeline.asset";
        public const string RendererAssetPath = GeneratedFolder + "/IslandRenderer.asset";

        // ---- ADR-03, as numbers rather than prose -------------------------------------------------

        public const int MsaaSamples = 2;
        public const int ShadowCascades = 1;
        public const float ShadowDistanceMetres = 15f;

        // ---- ADR-22, identity ---------------------------------------------------------------------

        /// <summary>
        /// The Android package id. <b>Final, never to change:</b> an update under another id
        /// installs as a second app and leaves every saved island behind with the first.
        /// </summary>
        public const string PackageId = "dev.islandia.app";

        /// <summary>The name under the icon in the headset's library. Free to change.</summary>
        public const string ProductName = "Islandia";

        /// <summary>
        /// Applies everything. Safe to run twice: the second run changes nothing.
        /// </summary>
        [MenuItem("Island/Apply project configuration")]
        public static void Apply()
        {
            try
            {
                var renderer = ConfigureRenderer();
                var pipeline = ConfigurePipeline(renderer);
                AssignPipeline(pipeline);
                ConfigurePlayer();
                XrConfigurator.Apply();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // The boot scene last: it is the one generated asset that depends on the others
                // (the camera needs the pipeline to exist). F-06.
                BootSceneBuilder.Build();
                Debug.Log($"[Island] Project configured. Pipeline at {PipelineAssetPath}, XR in {XrConfigurator.XrFolder}.");
            }
            catch (Exception error)
            {
                // Rethrown rather than logged so -executeMethod exits non-zero (ADR-06). An
                // exception that only reaches the log makes a red build look green.
                Debug.LogError($"[Island] Project configuration failed: {error}");
                throw;
            }
        }

        /// <summary>For <c>-executeMethod</c>, which wants a method that exits the editor itself.</summary>
        public static void ApplyAndExit()
        {
            try
            {
                Apply();
            }
            catch (Exception)
            {
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }

        // ---- the renderer -------------------------------------------------------------------------

        private static UniversalRendererData ConfigureRenderer()
        {
            var renderer = LoadOrCreate<UniversalRendererData>(RendererAssetPath);

            // Forward, never Forward+ or Deferred. On a Quest 2 the extra passes of the other two
            // buy nothing: there is one directional light and no deferred-shaded geometry.
            renderer.renderingMode = RenderingMode.Forward;

            // Depth priming costs a prepass to save overdraw. Mobile tile-based GPUs already
            // resolve overdraw cheaply, so it is a prepass bought for nothing.
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;

            // No camera post-processing (ADR-03). Leaving the data null is what switches the
            // feature off; an empty PostProcessData still allocates its passes.
            renderer.postProcessData = null;

            EditorUtility.SetDirty(renderer);
            return renderer;
        }

        // ---- the pipeline asset -------------------------------------------------------------------

        private static UniversalRenderPipelineAsset ConfigurePipeline(UniversalRendererData renderer)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                EnsureFolder(GeneratedFolder);
                // URP's own factory, so the asset arrives with whatever internal state it expects
                // rather than the bare defaults of CreateInstance.
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            // Properties with public setters.
            pipeline.supportsHDR = false;                    // no HDR on this hardware
            pipeline.msaaSampleCount = MsaaSamples;          // 2x
            pipeline.supportsCameraDepthTexture = false;     // nothing reads scene depth (ADR-05)
            pipeline.supportsCameraOpaqueTexture = false;    // nothing reads the opaque texture
            pipeline.useSRPBatcher = true;
            pipeline.supportsDynamicBatching = false;        // the SRP Batcher supersedes it
            pipeline.shadowDistance = ShadowDistanceMetres;
            pipeline.shadowCascadeCount = ShadowCascades;
            pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;

            // Properties whose setters are internal. Written by serialised field name, because
            // reflection on the private field would bypass serialisation and be lost on the next
            // write. The names come from UniversalRenderPipelineAsset's [SerializeField] fields.
            var serialised = new SerializedObject(pipeline);
            SetEnum(serialised, "m_MainLightRenderingMode", (int)LightRenderingMode.PerPixel);
            SetEnum(serialised, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.Disabled);
            SetBool(serialised, "m_MainLightShadowsSupported", true);
            serialised.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        private static void AssignPipeline(UniversalRenderPipelineAsset pipeline)
        {
            // Both, deliberately. The quality level's pipeline is what actually renders; the
            // graphics default is what a build falls back to when a quality level has none.
            QualitySettings.renderPipeline = pipeline;
            GraphicsSettings.defaultRenderPipeline = pipeline;
        }

        // ---- the player ---------------------------------------------------------------------------

        private static void ConfigurePlayer()
        {
            // ADR-22. Set before anything else so no build can ever be made under Unity's default
            // id, which would install as a different app from every build after it.
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.productName = ProductName;

            // Vulkan only. The Quest 2 runs it, and carrying GLES as a fallback means every shader
            // is compiled twice and the one nobody tests is the one that ships.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan });

            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // ASTC, which the Quest 2's GPU samples natively.
            //
            // Through PlayerSettings, not EditorUserBuildSettings.androidBuildSubtarget: that one
            // lives in Library/, which is not committed, so a test asserting it would pass on the
            // machine that ran this and fail on every other. This is the setting that travels.
            PlayerSettings.Android.textureCompressionFormats =
                new[] { TextureCompressionFormat.ASTC };
        }

        // ---- the plumbing -------------------------------------------------------------------------

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var created = ScriptableObject.CreateInstance<T>();
            if (created == null)
                throw new InvalidOperationException($"Could not create a {typeof(T).Name}.");

            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var built = parts[0];                       // "Assets"
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{built}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(built, parts[i]);
                built = next;
            }
        }

        private static void SetEnum(SerializedObject owner, string field, int value) =>
            Find(owner, field).enumValueIndex = value;

        private static void SetBool(SerializedObject owner, string field, bool value) =>
            Find(owner, field).boolValue = value;

        private static SerializedProperty Find(SerializedObject owner, string field)
        {
            var property = owner.FindProperty(field);
            if (property == null)
            {
                // A renamed field in a URP upgrade would otherwise fail silently and leave the
                // setting at its default, which is the exact fault this method exists to avoid.
                throw new InvalidOperationException(
                    $"'{field}' is not a serialised field of {owner.targetObject.GetType().Name} any " +
                    "more. URP has renamed it; read the new name out of the package sources " +
                    "artefact (F-07c) and update this.");
            }
            return property;
        }
    }
}
