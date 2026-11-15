using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Island.Runtime.Tests
{
    /// <summary>
    /// F-06, level L3: the committed boot scene loads in simulation mode, runs on scripted input
    /// and a scripted clock, and the log stays clean.
    /// </summary>
    /// <remarks>
    /// "Clean" is strict: any warning, error, assertion or exception logged while the scene loads
    /// and runs fails the test, whoever logged it. The test framework already fails on errors; a
    /// warning is let through by it and is caught here. If an engine warning ever has to be
    /// tolerated, it goes in <see cref="Tolerated"/> with the reason, never silently.
    /// </remarks>
    public class BootTests
    {
        private const string BootScenePath = "Assets/Island/Generated/Scenes/Boot.unity";
        private const int FramesToRun = 30;

        /// <summary>Warnings the boot may log without failing. Empty, and should stay so.</summary>
        private static readonly string[] Tolerated = { };

        private readonly List<string> _problems = new List<string>();

        [SetUp]
        public void Listen()
        {
            _problems.Clear();
            Application.logMessageReceived += Record;
        }

        [UnityTearDown]
        public IEnumerator StopListeningAndUnloadTheGame()
        {
            Application.logMessageReceived -= Record;
            CompositionRoot.ClearNextBoot();

            // Leave nothing booted for the next test: a second root in the same session is an
            // error by design, so the next boot must start from an empty world.
            var boot = SceneManager.GetSceneByPath(BootScenePath);
            if (boot.IsValid() && boot.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("Between tests"));
                yield return SceneManager.UnloadSceneAsync(boot);
            }
            Assert.That(CompositionRoot.Current, Is.Null, "the root outlived its scene");
        }

        private void Record(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Log) return;
            foreach (var tolerated in Tolerated)
                if (message.Contains(tolerated)) return;
            _problems.Add($"{type}: {message}");
        }

        [Test]
        public void BootSceneIsTheFirstSceneInTheBuild()
        {
            Assert.That(SceneUtility.GetBuildIndexByScenePath(BootScenePath), Is.EqualTo(0),
                $"{BootScenePath} must be build index 0. Run Island.Editor.ProjectConfigurator.ApplyAndExit.");
        }

        [UnityTest]
        public IEnumerator BootsInSimulationOnScriptedInputAndClock()
        {
            var clock = new ScriptedClock();
            var input = new ScriptedInputSource(clock, c => new InputFrame
            {
                HeadTracked = true,
                Head = new Pose(new Vector3(0f, 1.5f, c.Frame * 0.01f), Quaternion.Euler(0f, c.Frame, 0f)),
            });
            CompositionRoot.UseForNextBoot(SimulationPlatform.Create(input, clock));

            yield return SceneManager.LoadSceneAsync(BootScenePath, LoadSceneMode.Single);
            for (var i = 0; i < FramesToRun; i++) yield return null;

            var root = CompositionRoot.Current;
            Assert.That(root, Is.Not.Null, "no CompositionRoot booted");
            Assert.That(root.Services.Platform.IsSimulation, Is.True);
            Assert.That(root.Services.Clock, Is.SameAs(clock), "the scripted clock was not the one used");
            Assert.That(root.Services.Input, Is.SameAs(input), "the scripted input was not the one used");

            // The clock advanced once per frame the root ran, and at least once per frame waited.
            Assert.That(clock.Frame, Is.GreaterThanOrEqualTo(FramesToRun));
            Assert.That(clock.Seconds, Is.EqualTo(clock.Frame * ScriptedClock.DefaultStep).Within(1e-9));

            // The head is exactly where the script put it on the latest frame.
            var head = root.Services.Rig.Head;
            Assert.That(head, Is.SameAs(Camera.main.transform), "the rig's head is not the main camera");
            Assert.That(Vector3.Distance(head.localPosition, new Vector3(0f, 1.5f, clock.Frame * 0.01f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(head.localRotation, Quaternion.Euler(0f, clock.Frame, 0f)), Is.LessThan(0.01f));
            Assert.That(root.Services.LatestInput.Head.position.z, Is.EqualTo(clock.Frame * 0.01f).Within(1e-4f));

            AssertOneOfEach();
            Assert.That(_problems, Is.Empty, "the log was not clean:\n" + string.Join("\n", _problems));
        }

        [UnityTest]
        public IEnumerator BootsIntoTheIdleSimulationWhenNoHeadsetIsRunning()
        {
            // Nothing passed in: the root has to choose, and in the editor there is no XR loader.
            CompositionRoot.ClearNextBoot();

            yield return SceneManager.LoadSceneAsync(BootScenePath, LoadSceneMode.Single);
            for (var i = 0; i < 5; i++) yield return null;

            var root = CompositionRoot.Current;
            Assert.That(root, Is.Not.Null, "no CompositionRoot booted");
            Assert.That(root.Services.Platform.IsSimulation, Is.True, "booted a headset in the editor");
            Assert.That(root.Services.Device.Description, Is.EqualTo("simulation"));
            Assert.That(root.Services.Rig.Head.localPosition.y, Is.EqualTo(1.6f).Within(1e-4f),
                "the idle player should be standing, eyes at 1.6 m");

            AssertOneOfEach();
            Assert.That(_problems, Is.Empty, "the log was not clean:\n" + string.Join("\n", _problems));
        }

        private static void AssertOneOfEach()
        {
            Assert.That(Object.FindObjectsByType<CompositionRoot>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<SceneRig>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }
    }
}
