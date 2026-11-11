using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Island.Editor.Tests
{
    /// <summary>
    /// Asserts every ADR-03 item by reading the committed assets back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reading back matters more than it sounds. ADR-06 notes that batchmode builds have
    /// historically reset generated settings, and ADR-03 says every item is set by code *and*
    /// asserted by a test. A test that called the configurator and then checked the object it
    /// returned would prove the setter works and nothing about what is on disk — so these load the
    /// asset by path, which is what a build will load.
    /// </para>
    /// <para>
    /// No test here calls <c>Apply</c>. It writes to the project, and a test that mutates the
    /// project it is checking cannot tell a committed setting from one it has just made.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class ProjectConfigurationTests
    {
        private UniversalRenderPipelineAsset _pipeline;
        private UniversalRendererData _renderer;

        [OneTimeSetUp]
        public void LoadWhatWasCommitted()
        {
            _pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                ProjectConfigurator.PipelineAssetPath);
            _renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                ProjectConfigurator.RendererAssetPath);

            Assert.That(_pipeline, Is.Not.Null,
                $"No pipeline asset at {ProjectConfigurator.PipelineAssetPath}. Run "
                + "Island.Editor.ProjectConfigurator.ApplyAndExit and commit what it writes.");
            Assert.That(_renderer, Is.Not.Null,
                $"No renderer asset at {ProjectConfigurator.RendererAssetPath}.");
        }

        // ---- the pipeline asset -------------------------------------------------------------------

        [Test]
        public void TheGeneratedAssetsAreWhereAdr06SaysTheyAre()
        {
            Assert.That(ProjectConfigurator.PipelineAssetPath,
                Does.StartWith("Assets/Island/Generated/"),
                "ADR-06 puts generated assets under Assets/Island/Generated/");
            Assert.That(File.Exists(ProjectConfigurator.PipelineAssetPath), Is.True);
            Assert.That(File.Exists(ProjectConfigurator.RendererAssetPath), Is.True);
        }

        [Test]
        public void ThisPipelineIsTheOneThatRenders()
        {
            // An asset configured perfectly and not assigned renders nothing.
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(_pipeline),
                "the quality level's pipeline is what actually renders");
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(_pipeline),
                "the graphics default is the fallback when a quality level has none");
        }

        [Test]
        public void HdrIsOff() => Assert.That(_pipeline.supportsHDR, Is.False);

        [Test]
        public void MsaaIsTwoSamples() =>
            Assert.That(_pipeline.msaaSampleCount, Is.EqualTo(2));

        [Test]
        public void NothingReadsSceneDepth()
        {
            // ADR-05: shoreline foam comes from the heightfield uploaded as a texture, not from a
            // depth read. A depth texture nobody samples is a resolve per frame for nothing.
            Assert.That(_pipeline.supportsCameraDepthTexture, Is.False);
        }

        [Test]
        public void NothingReadsTheOpaqueTexture() =>
            Assert.That(_pipeline.supportsCameraOpaqueTexture, Is.False);

        [Test]
        public void TheSrpBatcherIsOn() => Assert.That(_pipeline.useSRPBatcher, Is.True);

        [Test]
        public void DynamicBatchingIsOff()
        {
            // The SRP Batcher supersedes it, and having both costs a CPU pass that changes nothing.
            Assert.That(_pipeline.supportsDynamicBatching, Is.False);
        }

        [Test]
        public void TheGpuResidentDrawerIsOff() =>
            Assert.That(_pipeline.gpuResidentDrawerMode, Is.EqualTo(GPUResidentDrawerMode.Disabled));

        [Test]
        public void ShadowsAreOneCascadeAtFifteenMetres()
        {
            Assert.That(_pipeline.shadowCascadeCount, Is.EqualTo(1));
            Assert.That(_pipeline.shadowDistance, Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void ThereIsOneDirectionalLightAndNoOthers()
        {
            // Written through SerializedObject because these setters are internal. Read back the
            // same way, so the test proves what is serialised rather than what a property returns.
            var serialised = new SerializedObject(_pipeline);

            Assert.That(serialised.FindProperty("m_MainLightRenderingMode").enumValueIndex,
                Is.EqualTo((int)LightRenderingMode.PerPixel));
            Assert.That(serialised.FindProperty("m_AdditionalLightsRenderingMode").enumValueIndex,
                Is.EqualTo((int)LightRenderingMode.Disabled),
                "ADR-04 allows exactly one directional light");
            Assert.That(serialised.FindProperty("m_MainLightShadowsSupported").boolValue, Is.True);
        }

        [Test]
        public void TheInternalSettersWereWrittenThroughSerialisation()
        {
            // The point of criterion three. Reflection on the private field would set the value in
            // memory and lose it on the next write, and the property would still read correctly in
            // the same session — so this checks the serialised data and the property agree.
            var serialised = new SerializedObject(_pipeline);
            Assert.That(serialised.FindProperty("m_MainLightRenderingMode").enumValueIndex,
                Is.EqualTo((int)_pipeline.mainLightRenderingMode),
                "the serialised value and the property disagree, which means one of them was "
                + "written around the other");
        }

        // ---- the renderer -------------------------------------------------------------------------

        [Test]
        public void TheRendererIsForwardNotForwardPlusOrDeferred() =>
            Assert.That(_renderer.renderingMode, Is.EqualTo(RenderingMode.Forward));

        [Test]
        public void DepthPrimingIsOff() =>
            Assert.That(_renderer.depthPrimingMode, Is.EqualTo(DepthPrimingMode.Disabled));

        [Test]
        public void ThereIsNoCameraPostProcessing()
        {
            // Null is what switches it off; an empty PostProcessData still allocates its passes.
            Assert.That(_renderer.postProcessData, Is.Null);
        }

        [Test]
        public void ThePipelineUsesThisRenderer()
        {
            var serialised = new SerializedObject(_pipeline);
            var list = serialised.FindProperty("m_RendererDataList");
            Assert.That(list, Is.Not.Null, "URP has renamed m_RendererDataList");
            Assert.That(list.arraySize, Is.GreaterThan(0), "the pipeline has no renderer at all");

            var assigned = false;
            for (var i = 0; i < list.arraySize; i++)
            {
                if (ReferenceEquals(list.GetArrayElementAtIndex(i).objectReferenceValue, _renderer))
                    assigned = true;
            }
            Assert.That(assigned, Is.True,
                "the committed renderer is not the one the committed pipeline uses");
        }

        // ---- render graph -------------------------------------------------------------------------

        [Test]
        public void RenderGraphIsOn()
        {
            // Asserted, not set. In this editor version URP's enableRenderCompatibilityMode reads
            // false unconditionally and its setter is [Obsolete(..., error: true)] — assigning it
            // does not compile. If a later URP makes it settable again, this test is what will
            // notice that the default moved.
            Assert.That(GraphicsSettings.TryGetRenderPipelineSettings<RenderGraphSettings>(
                out var settings), Is.True, "URP's RenderGraphSettings are not registered");
            Assert.That(settings.enableRenderCompatibilityMode, Is.False,
                "compatibility mode means Render Graph is off, which ADR-03 forbids");
        }

        // ---- the player ---------------------------------------------------------------------------

        [Test]
        public void AndroidRendersWithVulkanAndNothingElse()
        {
            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), Is.False);

            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            Assert.That(apis, Is.EqualTo(new[] { GraphicsDeviceType.Vulkan }),
                "carrying GLES as a fallback compiles every shader twice, and the variant nobody "
                + "tests is the one that ships");
        }

        [Test]
        public void ColourIsLinear() =>
            Assert.That(PlayerSettings.colorSpace, Is.EqualTo(ColorSpace.Linear));

        [Test]
        public void TheScriptingBackendIsIl2cppOnArm64()
        {
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
                Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(PlayerSettings.Android.targetArchitectures,
                Is.EqualTo(AndroidArchitecture.ARM64),
                "ARM64 only: ARMv7 doubles the build and the Quest 2 does not need it");
        }

        [Test]
        public void TexturesAreAstc()
        {
            // Read from PlayerSettings rather than EditorUserBuildSettings, because the latter
            // lives in Library/ and is not committed: asserting it would pass on the machine that
            // ran the configurator and fail on every other, including this CI run.
            Assert.That(PlayerSettings.Android.textureCompressionFormats,
                Is.EqualTo(new[] { TextureCompressionFormat.ASTC }));
        }

        // ---- idempotence ---------------------------------------------------------------------------

        [Test]
        public void TheConfiguratorsConstantsMatchAdr03()
        {
            // The configurator and this test read the same constants, so a change to ADR-03 that
            // touches one and not the other cannot pass unnoticed.
            Assert.That(ProjectConfigurator.MsaaSamples, Is.EqualTo(2));
            Assert.That(ProjectConfigurator.ShadowCascades, Is.EqualTo(1));
            Assert.That(ProjectConfigurator.ShadowDistanceMetres, Is.EqualTo(15f).Within(0.001f));
        }
    }
}
