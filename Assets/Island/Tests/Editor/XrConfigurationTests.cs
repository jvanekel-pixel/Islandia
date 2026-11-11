using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Island.Editor.Tests
{
    /// <summary>
    /// Asserts the OpenXR setup (F-05) by reading the committed XR assets back from disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ADR-06: batchmode builds have historically reset XR settings, so they are generated once,
    /// committed, and asserted. Everything here is loaded by path from <c>Assets/XR</c> — the
    /// files a build will read — and nothing calls the configurator, because a test that wrote the
    /// settings could not tell a committed value from one it had just made. If an editor run or a
    /// build rewrites these files, the next run of this suite reads the rewritten files and fails;
    /// the <c>unity-tests</c> workflow also refuses a run that leaves <c>Assets/XR</c> changed.
    /// </para>
    /// <para>
    /// The OpenXR settings are found through the package settings asset's own serialised lists
    /// rather than <c>OpenXRSettings.GetSettingsForBuildTargetGroup</c>, which returns nothing on
    /// an editor without the Android module and would turn a real check into a skipped one.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class XrConfigurationTests
    {
        private OpenXRSettings _android;
        private Dictionary<string, OpenXRFeature> _features;

        [OneTimeSetUp]
        public void LoadWhatWasCommitted()
        {
            Assert.That(File.Exists(XrConfigurator.OpenXrPackageSettingsPath), Is.True,
                $"No OpenXR settings at {XrConfigurator.OpenXrPackageSettingsPath}. Run "
                + "Island.Editor.ProjectConfigurator.ApplyAndExit and commit what it writes.");

            _android = LoadOpenXrSettings(BuildTargetGroup.Android);
            Assert.That(_android, Is.Not.Null, "the OpenXR package settings hold nothing for Android");

            _features = new Dictionary<string, OpenXRFeature>();
            foreach (var feature in _android.GetFeatures())
            {
                if (feature == null) continue;
                _features[XrConfigurator.FeatureId(feature)] = feature;
            }
        }

        // ---- the loader ---------------------------------------------------------------------------

        [Test]
        public void TheXrSettingsAreCommittedUnderAssetsXr()
        {
            Assert.That(File.Exists(XrConfigurator.GeneralSettingsPath), Is.True);
            Assert.That(File.Exists(XrConfigurator.OpenXrPackageSettingsPath), Is.True);
            Assert.That(XrConfigurator.GeneralSettingsPath, Does.StartWith("Assets/XR/"));
        }

        [Test]
        public void AndroidLoadsOpenXrAndNothingElse()
        {
            var general = LoadGeneralSettings().SettingsForBuildTarget(BuildTargetGroup.Android);
            Assert.That(general, Is.Not.Null, "no XR settings for Android");
            Assert.That(general.Manager, Is.Not.Null, "no loader list for Android");

            var loaders = general.Manager.activeLoaders.Where(l => l != null)
                .Select(l => l.GetType().FullName).ToArray();
            Assert.That(loaders, Is.EqualTo(new[] { XrConfigurator.OpenXrLoaderType }),
                "OpenXR is the one loader (ADR-01: no Oculus XR plug-in, no OVRPlugin)");
        }

        [Test]
        public void XrStartsWithTheAppOnAndroid()
        {
            var general = LoadGeneralSettings().SettingsForBuildTarget(BuildTargetGroup.Android);
            Assert.That(general.InitManagerOnStart, Is.True,
                "without this the app launches flat on the headset");
        }

        [Test]
        public void TheEditorAndPlayModeTestsRunWithoutXr()
        {
            // ADR-07: PlayMode tests run through the simulation implementations, with no loader.
            var standalone = LoadGeneralSettings().SettingsForBuildTarget(BuildTargetGroup.Standalone);
            var loaders = standalone?.Manager?.activeLoaders.Where(l => l != null).ToArray()
                          ?? new XRLoader[0];
            Assert.That(loaders, Is.Empty, "a Standalone loader would start XR in the editor");
        }

        [Test]
        public void TheBuildCanFindTheSettings()
        {
            // A build reads both through EditorBuildSettings' config objects, which live in
            // ProjectSettings/EditorBuildSettings.asset. Settings on disk that are not registered
            // there are settings the build does not see.
            Assert.That(EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                out XRGeneralSettingsPerBuildTarget general), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(general), Is.EqualTo(XrConfigurator.GeneralSettingsPath));

            Assert.That(EditorBuildSettings.TryGetConfigObject(XrConfigurator.OpenXrSettingsKey,
                out Object openXr), Is.True, "OpenXR's package settings are not registered");
            Assert.That(AssetDatabase.GetAssetPath(openXr), Is.EqualTo(XrConfigurator.OpenXrPackageSettingsPath));
        }

        // ---- rendering ----------------------------------------------------------------------------

        [Test]
        public void StereoIsMultiview()
        {
            // Single Pass Instanced is what OpenXR calls multiview on Android (ADR-03).
            Assert.That(_android.renderMode, Is.EqualTo(OpenXRSettings.RenderMode.SinglePassInstanced));
        }

        [Test]
        public void FoveationIsDrivenByUrp()
        {
            Assert.That(_android.foveatedRenderingApi,
                Is.EqualTo(OpenXRSettings.BackendFovationApi.SRPFoveation));
            AssertEnabled(XrConfigurator.FoveatedRendering);
        }

        [Test]
        public void NoDepthIsSubmitted()
        {
            // DECISIONS.md 2026-11-11: no consumer of submitted depth exists yet, and submitting it
            // stores the depth buffer out of tile memory every frame.
            Assert.That(_android.depthSubmissionMode, Is.EqualTo(OpenXRSettings.DepthSubmissionMode.None));
        }

        // ---- features -----------------------------------------------------------------------------

        [Test]
        public void MetaQuestSupportIsOn() => AssertEnabled(XrConfigurator.MetaQuestSupport);

        [Test]
        public void TheQuest2IsATargetDevice()
        {
            // ADR-02: the Quest 2 is the target. Its manifest name is "quest2".
            var devices = new SerializedObject(_features[XrConfigurator.MetaQuestSupport])
                .FindProperty("targetDevices");
            Assert.That(devices, Is.Not.Null, "Meta Quest Support has renamed targetDevices");

            var quest2 = false;
            for (var i = 0; i < devices.arraySize; i++)
            {
                var device = devices.GetArrayElementAtIndex(i);
                if (device.FindPropertyRelative("manifestName").stringValue == "quest2")
                    quest2 = device.FindPropertyRelative("enabled").boolValue;
            }
            Assert.That(quest2, Is.True, "the Quest 2 is not an enabled target device");
        }

        [Test]
        public void HandTrackingIsOn()
        {
            AssertEnabled(XrConfigurator.HandTrackingSubsystem);
            Assert.That(_features[XrConfigurator.HandTrackingSubsystem].GetType().FullName,
                Is.EqualTo("UnityEngine.XR.Hands.OpenXR.HandTracking"));
        }

        [Test]
        public void DisplayUtilitiesAreOn()
        {
            // Without this feature a refresh-rate request silently does nothing (audit item 8).
            AssertEnabled(XrConfigurator.MetaDisplayUtilities);
            Assert.That(_features.TryGetValue(XrConfigurator.MetaLifeCycleHelper, out var helper)
                        && helper.enabled, Is.True,
                "Meta's life-cycle helper is off, so Meta's features will not receive callbacks");
        }

        [Test]
        public void NothingElseIsOn()
        {
            var unexpected = _features
                .Where(kv => kv.Value.enabled
                             && !XrConfigurator.IsChosen(kv.Key)
                             && !XrConfigurator.IsPackageHelper(kv.Key))
                .Select(kv => kv.Key).ToArray();
            Assert.That(unexpected, Is.Empty,
                "every enabled feature adds extensions, permissions and manifest entries");
        }

        // ---- interaction profiles -----------------------------------------------------------------

        [Test]
        public void TouchControllersAreAnInteractionProfile() =>
            AssertProfile(XrConfigurator.OculusTouchProfile);

        [Test]
        public void HandsAreAnInteractionProfile() =>
            AssertProfile(XrConfigurator.HandInteractionProfile);

        // ---- plumbing -----------------------------------------------------------------------------

        private void AssertEnabled(string id)
        {
            Assert.That(_features.ContainsKey(id), Is.True, $"no feature '{id}' in the Android settings");
            Assert.That(_features[id].enabled, Is.True, $"'{id}' is off");
        }

        private void AssertProfile(string id)
        {
            AssertEnabled(id);
            Assert.That(_features[id], Is.InstanceOf<OpenXRInteractionFeature>(),
                $"'{id}' is not an interaction profile");
        }

        private static XRGeneralSettingsPerBuildTarget LoadGeneralSettings()
        {
            var general = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                XrConfigurator.GeneralSettingsPath);
            Assert.That(general, Is.Not.Null, $"no XR Management settings at {XrConfigurator.GeneralSettingsPath}");
            return general;
        }

        private static OpenXRSettings LoadOpenXrSettings(BuildTargetGroup group)
        {
            var package = AssetDatabase.LoadMainAssetAtPath(XrConfigurator.OpenXrPackageSettingsPath);
            Assert.That(package, Is.Not.Null);

            var serialised = new SerializedObject(package);
            var keys = serialised.FindProperty("Keys");
            var values = serialised.FindProperty("Values");
            Assert.That(keys, Is.Not.Null, "OpenXR has renamed its package settings' Keys list");
            Assert.That(values, Is.Not.Null, "OpenXR has renamed its package settings' Values list");

            for (var i = 0; i < Mathf.Min(keys.arraySize, values.arraySize); i++)
            {
                if (keys.GetArrayElementAtIndex(i).intValue == (int)group)
                    return values.GetArrayElementAtIndex(i).objectReferenceValue as OpenXRSettings;
            }
            return null;
        }
    }
}
