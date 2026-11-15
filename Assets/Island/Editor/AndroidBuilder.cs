using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Island.Editor
{
    /// <summary>
    /// Builds the Quest APK headlessly (F-07). Run by the <c>android-build</c> workflow:
    /// <c>-buildTarget Android -executeMethod Island.Editor.AndroidBuilder.BuildAndExit -islandOutput &lt;path&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It changes no setting. Everything a build needs is already committed by
    /// <see cref="ProjectConfigurator"/>, and the workflow fails if the build leaves a setting,
    /// a <c>.meta</c> or the XR assets different from what was committed (ADR-06). A builder
    /// that "fixed up" settings on the way would hide exactly the drift that check exists for.
    /// </para>
    /// <para>
    /// The editor must be opened for Android (<c>-buildTarget Android</c>) rather than switched
    /// here: switching re-imports the project inside the build call, which is slow and is where
    /// batchmode builds have historically lost their XR settings.
    /// </para>
    /// <para>
    /// Signing with the owner's key is <c>R-01</c>'s; until then the APK carries Unity's debug
    /// key, which is enough to sideload it for HG-1.
    /// </para>
    /// </remarks>
    public static class AndroidBuilder
    {
        public const string DefaultOutput = "Builds/Android/Islandia.apk";
        public const string OutputArgument = "-islandOutput";

        /// <summary>What the build will be asked for: every enabled scene in the build list, an APK.</summary>
        public static BuildPlayerOptions Options(string output) => new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        /// <summary>Builds the APK, or throws saying why not.</summary>
        public static BuildReport Build(string output)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException(
                    $"The editor is targeting {EditorUserBuildSettings.activeBuildTarget}. Open it with -buildTarget Android.");

            var options = Options(output);
            if (options.scenes.Length == 0)
                throw new BuildFailedException("There is no scene in the build. Run ProjectConfigurator.ApplyAndExit (F-06).");

            // An APK, not an app bundle: it is sideloaded onto one headset, never sent to a store.
            // This lives in Library/, so setting it changes nothing that is committed.
            EditorUserBuildSettings.buildAppBundle = false;

            var folder = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException(
                    $"The Android build {summary.result} with {summary.totalErrors} error(s). The log above says which.");

            Debug.Log($"[Island] APK built: {output}, {summary.totalSize / (1024.0 * 1024.0):0.0} MB, " +
                      $"in {summary.totalTime.TotalMinutes:0.0} min, {summary.totalWarnings} warning(s).");
            return report;
        }

        /// <summary>For <c>-executeMethod</c>: exits 0 on a built APK and 1 on anything else.</summary>
        public static void BuildAndExit()
        {
            try
            {
                Build(OutputPath(Environment.GetCommandLineArgs()));
            }
            catch (Exception error)
            {
                Debug.LogError($"[Island] Android build failed: {error}");
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }

        /// <summary>The value after <see cref="OutputArgument"/>, or <see cref="DefaultOutput"/>.</summary>
        public static string OutputPath(string[] arguments)
        {
            var index = Array.IndexOf(arguments, OutputArgument);
            if (index < 0) return DefaultOutput;
            if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-"))
                throw new ArgumentException($"{OutputArgument} needs a path after it.");
            return arguments[index + 1];
        }
    }
}
