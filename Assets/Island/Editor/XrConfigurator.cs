using System;
using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Island.Editor
{
    /// <summary>
    /// Sets up OpenXR for the Quest 2 in code (ADR-01, ADR-03, ADR-06): the loader on Android, the
    /// render mode, foveation, and exactly the OpenXR features and interaction profiles the game
    /// uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called from <see cref="ProjectConfigurator.Apply"/>, so one <c>-executeMethod</c> configures
    /// the whole project and the <c>unity-configure</c> workflow commits all of it.
    /// </para>
    /// <para>
    /// Every API used here is public and was read from the resolved package sources (F-07c):
    /// <c>com.unity.xr.management</c> 4.7.0 and <c>com.unity.xr.openxr</c> 1.19.0. Features are
    /// found by their published feature id through <see cref="FeatureHelpers"/>, not by type, so
    /// this assembly references neither the Meta OpenXR nor the XR Hands assembly — a rename of a
    /// class there cannot break the editor's compile, and an id that disappears throws here
    /// instead.
    /// </para>
    /// <para>
    /// <b>The Android module must be installed.</b> OpenXR only creates settings for a build target
    /// the editor can build for, so on an editor without Android this would find no Android
    /// settings at all. It throws rather than quietly configuring nothing.
    /// </para>
    /// <para>
    /// Standalone gets no loader on purpose: the editor and the PlayMode tests run without XR,
    /// through the simulation implementations ADR-07 requires.
    /// </para>
    /// </remarks>
    public static class XrConfigurator
    {
        /// <summary>Where XR Management keeps its settings. Committed (ADR-06).</summary>
        public const string XrFolder = "Assets/XR";

        public const string GeneralSettingsPath = XrFolder + "/XRGeneralSettingsPerBuildTarget.asset";

        /// <summary>Where the OpenXR package puts its settings; the package decides this path.</summary>
        public const string OpenXrPackageSettingsPath = XrFolder + "/Settings/OpenXR Package Settings.asset";

        /// <summary>The key OpenXR registers its settings under in EditorBuildSettings.</summary>
        /// <remarks>The package's own constant (<c>Constants.k_SettingsKey</c>) is internal.</remarks>
        public const string OpenXrSettingsKey = "com.unity.xr.openxr.settings4";

        public const string OpenXrLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";

        // ---- the feature set, by published id ----------------------------------------------------

        /// <summary>Meta Quest Support — the Android manifest, the Quest target devices.</summary>
        public const string MetaQuestSupport = "com.unity.openxr.feature.metaquest";

        /// <summary>Foveated Rendering, driven through URP (SRP Foveation).</summary>
        public const string FoveatedRendering = "com.unity.openxr.feature.foveatedrendering";

        /// <summary>XR Hands' hand tracking subsystem.</summary>
        public const string HandTrackingSubsystem = "com.unity.openxr.feature.input.handtrackingsubsystem";

        /// <summary>Meta Quest: Display Utilities — needed to request a refresh rate (audit item 8).</summary>
        public const string MetaDisplayUtilities = "com.unity.openxr.feature.meta-display-utilities";

        /// <summary>Oculus Touch controllers, which is what ships with a Quest 2.</summary>
        public const string OculusTouchProfile = "com.unity.openxr.feature.input.oculustouch";

        /// <summary>Hands as an input device (XR_EXT_hand_interaction).</summary>
        public const string HandInteractionProfile = "com.unity.openxr.feature.input.handinteraction";

        /// <summary>Every feature and profile this project turns on for Android. Nothing else is on.</summary>
        public static readonly string[] EnabledAndroidFeatures =
        {
            MetaQuestSupport,
            FoveatedRendering,
            HandTrackingSubsystem,
            MetaDisplayUtilities,
            OculusTouchProfile,
            HandInteractionProfile,
        };

        /// <summary>
        /// Applies the XR configuration. Idempotent: a second run changes nothing.
        /// Throws on any failure (ADR-06).
        /// </summary>
        public static void Apply()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException(
                    "The Android module is not installed in this editor, so OpenXR will not create "
                    + "Android settings. Run this on the unityci/editor android image.");
            }

            ConfigureLoader();
            ConfigureOpenXr();
        }

        // ---- XR Management: the loader ------------------------------------------------------------

        private static void ConfigureLoader()
        {
            var perTarget = LoadOrCreateGeneralSettings();

            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            EditorUtility.SetDirty(general);

            var manager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.IsLoaderAssigned(OpenXrLoaderType, BuildTargetGroup.Android)
                && !XRPackageMetadataStore.AssignLoader(manager, OpenXrLoaderType, BuildTargetGroup.Android))
            {
                throw new InvalidOperationException(
                    $"XR Management refused to assign {OpenXrLoaderType} for Android.");
            }

            // AssignLoader can report success and still leave the loader out, when the package
            // metadata does not list it for the target. Check what actually happened.
            if (!XRPackageMetadataStore.IsLoaderAssigned(OpenXrLoaderType, BuildTargetGroup.Android))
            {
                throw new InvalidOperationException(
                    $"{OpenXrLoaderType} is not among Android's loaders after assigning it.");
            }

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(perTarget);
        }

        private static XRGeneralSettingsPerBuildTarget LoadOrCreateGeneralSettings()
        {
            // The package's own GetOrCreate is internal, so this does what it does: one asset at
            // the package's default path, registered under the key the build reads it from.
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(GeneralSettingsPath);
            if (perTarget == null)
            {
                EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perTarget);
                if (perTarget != null && AssetDatabase.GetAssetPath(perTarget) != GeneralSettingsPath)
                {
                    throw new InvalidOperationException(
                        $"XR Management settings exist at {AssetDatabase.GetAssetPath(perTarget)}, "
                        + $"not {GeneralSettingsPath}. Move or delete them, then run this again.");
                }
            }

            if (perTarget == null)
            {
                EnsureFolder(XrFolder);
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, GeneralSettingsPath);
                AssetDatabase.SaveAssets();
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            return perTarget;
        }

        // ---- OpenXR: render settings and features -------------------------------------------------

        private static void ConfigureOpenXr()
        {
            // Creates the package settings asset if it is missing, and a feature object for every
            // OpenXR feature the project's packages define. This is the public route to both.
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);

            // Standalone too, with every feature left off and no loader. The OpenXR package
            // creates these on its own the first time anything asks for them; creating them here
            // means that happens in the run that commits, not in a later editor session that
            // would leave Assets/XR changed behind it.
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);

            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
                throw new InvalidOperationException("OpenXR created no settings for Android.");

            // Single-pass instanced is multiview on Android: one draw for both eyes (ADR-03).
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;

            // URP drives foveation (ADR-03: foveated rendering on). The legacy API covers fewer
            // render targets under URP.
            settings.foveatedRenderingApi = OpenXRSettings.BackendFovationApi.SRPFoveation;

            // No depth submission. Nothing composites against scene depth and Application
            // SpaceWarp sits behind a no-op interface (audit, phase 1–3, item 2); submitting depth
            // would store the depth buffer out of tile memory every frame for nothing.
            // DECISIONS.md, 2026-11-11.
            settings.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.None;

            EditorUtility.SetDirty(settings);

            // Everything this project did not choose goes off first, then the chosen set goes on.
            // In that order because enabling a Meta feature is what switches on Meta's hidden
            // life-cycle helper; disabling afterwards would undo that. A feature nobody chose still
            // adds its extensions, permissions and manifest entries to the build.
            foreach (var feature in settings.GetFeatures())
            {
                if (feature == null || !feature.enabled) continue;
                var id = FeatureId(feature);
                if (IsChosen(id) || IsPackageHelper(id)) continue;
                feature.enabled = false;
                EditorUtility.SetDirty(feature);
            }

            foreach (var id in EnabledAndroidFeatures)
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
                if (feature == null)
                {
                    throw new InvalidOperationException(
                        $"No OpenXR feature with id '{id}' for Android. A package has renamed or "
                        + "removed it; read the new id out of the package sources artefact (F-07c).");
                }
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }

            foreach (var id in EnabledAndroidFeatures)
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
                if (feature == null || !feature.enabled)
                {
                    throw new InvalidOperationException(
                        $"OpenXR feature '{id}' would not stay enabled. Something it depends on is off.");
                }
            }
        }

        /// <summary>
        /// Hidden features a package manages itself. Meta OpenXR switches its life-cycle helper on
        /// whenever any Meta feature is on (Display Utilities is one), and the Meta features do not
        /// work without it, so it is left to the package rather than forced either way.
        /// </summary>
        public static readonly string[] PackageHelpers = { MetaLifeCycleHelper };

        public const string MetaLifeCycleHelper = "MetaOpenXR-OpenXRLifeCycle";

        public static bool IsChosen(string id) =>
            Array.Exists(EnabledAndroidFeatures, x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));

        public static bool IsPackageHelper(string id) =>
            Array.Exists(PackageHelpers, x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>A feature's published id, read from its serialised field (the property is internal).</summary>
        public static string FeatureId(OpenXRFeature feature)
        {
            var property = new SerializedObject(feature).FindProperty("featureIdInternal");
            if (property == null)
            {
                throw new InvalidOperationException(
                    "OpenXRFeature has no serialised 'featureIdInternal' any more. OpenXR has renamed "
                    + "it; read the new name out of the package sources artefact (F-07c).");
            }
            return property.stringValue;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
