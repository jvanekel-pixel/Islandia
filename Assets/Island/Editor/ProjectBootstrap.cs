#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Island.Editor
{
    /// <summary>
    /// Adds the packages the project needs, and reports what the resolver chose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Packages are added <b>by name, without a version</b>. Unity's resolver then picks the
    /// newest release compatible with this editor, and <c>packages-lock.json</c> records exactly
    /// what it picked — which is both more accurate and more durable than a version number copied
    /// out of a documentation page that may already have moved. The lock file is the pin.
    /// </para>
    /// <para>
    /// Run from CI, never by hand:
    /// <c>-executeMethod Island.Editor.ProjectBootstrap.AddPackages</c>. It exits non-zero if any
    /// package cannot be resolved, so a typo or a renamed package fails the build rather than
    /// quietly leaving the project short of something.
    /// </para>
    /// </remarks>
    public static class ProjectBootstrap
    {
        /// <summary>
        /// Everything the project needs from Unity's registry, with why it is here.
        /// </summary>
        /// <remarks>
        /// Kept in one list so that adding a dependency is a visible, reviewable change rather
        /// than an edit buried in a manifest. See <c>docs/plan/02-ARCHITECTURE.md</c> ADR-01.
        /// </remarks>
        private static readonly (string Name, string Why)[] Required =
        {
            ("com.unity.render-pipelines.universal", "URP: the render pipeline, ADR-03"),
            ("com.unity.xr.management",              "loads the XR plugin at startup"),
            ("com.unity.xr.openxr",                  "OpenXR: how the headset is spoken to"),
            ("com.unity.xr.meta-openxr",             "Meta's OpenXR features: foveation, display"),
            ("com.unity.xr.interaction.toolkit",     "controllers, rays, grabbing, locomotion"),
            ("com.unity.xr.hands",                   "hand tracking"),
            ("com.unity.inputsystem",                "the input backend XRI needs"),
            ("com.unity.burst",                      "compiles the terrain jobs to native code"),
            ("com.unity.collections",                "native containers those jobs use"),
            ("com.unity.mathematics",                "the maths types Burst understands"),
            ("com.unity.ai.navigation",              "pet navigation, ADR-16"),
            ("com.unity.cloud.gltfast",              "loading the player's own models, ADR-12"),
            ("com.unity.cloud.ktx",                  "KTX2 textures inside those models"),
            ("com.unity.cloud.draco",                "Draco-compressed meshes inside those models"),
            ("com.unity.nuget.newtonsoft-json",      "JSON for the StreamBox client, DECISIONS 2026-11-10"),
            ("com.unity.test-framework",             "the test runner CI drives"),
        };

        public static void AddPackages()
        {
            var failures = new List<string>();

            foreach (var (name, why) in Required)
            {
                Debug.Log($"[bootstrap] adding {name}  ({why})");
                var request = Client.Add(name);
                if (!Wait(request, name, failures)) continue;
                Debug.Log($"[bootstrap] {name} resolved to {request.Result.version}");
            }

            if (failures.Count > 0)
            {
                Debug.LogError("[bootstrap] these packages could not be added:\n  " +
                               string.Join("\n  ", failures));
                Exit(1);
                return;
            }

            Report();
            Exit(0);
        }

        /// <summary>Writes what is actually installed, for the commit message and the record.</summary>
        public static void Report()
        {
            var list = Client.List(offlineMode: true, includeIndirectDependencies: false);
            if (!Wait(list, "the installed package list", null)) { Exit(1); return; }

            var lines = list.Result
                .Where(package => package.source != PackageSource.BuiltIn)
                .OrderBy(package => package.name)
                .Select(package => $"| `{package.name}` | {package.version} |");

            Debug.Log("[bootstrap] resolved packages\n" +
                      "| Package | Version |\n| :-- | :-- |\n" + string.Join("\n", lines));
        }

        /// <summary>
        /// Pumps a package request to completion.
        /// </summary>
        /// <remarks>
        /// Batch mode has no editor loop to tick these for us, so they are pumped here. The
        /// timeout matters: a registry that stops answering would otherwise hang the job until
        /// the runner's own limit, twenty minutes later, with nothing in the log to say why.
        /// </remarks>
        private static bool Wait<T>(T request, string what, List<string> failures)
            where T : Request
        {
            var deadline = DateTime.UtcNow.AddMinutes(5);
            while (!request.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                {
                    var message = $"{what}: timed out after five minutes";
                    Debug.LogError("[bootstrap] " + message);
                    failures?.Add(message);
                    return false;
                }
                System.Threading.Thread.Sleep(100);
            }

            if (request.Status == StatusCode.Success) return true;

            var error = $"{what}: {request.Error?.message ?? "unknown error"}";
            Debug.LogError("[bootstrap] " + error);
            failures?.Add(error);
            return false;
        }

        private static void Exit(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
#endif
