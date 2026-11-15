using System.IO;
using Island.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Island.Editor.Tests
{
    /// <summary>
    /// F-06, level L2: the committed boot scene is the one <see cref="BootSceneBuilder"/> makes,
    /// it is the build's only scene, and running the builder again writes nothing.
    /// </summary>
    public class BootSceneTests
    {
        private static readonly string[] Files =
        {
            BootSceneBuilder.BootScenePath,
            BootSceneBuilder.BootScenePath + ".meta",
            "ProjectSettings/EditorBuildSettings.asset",
        };

        [OneTimeSetUp]
        public void TheSceneIsCommitted()
        {
            Assert.That(File.Exists(BootSceneBuilder.BootScenePath), Is.True,
                $"No boot scene at {BootSceneBuilder.BootScenePath}. Run "
                + "Island.Editor.ProjectConfigurator.ApplyAndExit and commit what it writes.");
        }

        [Test]
        public void CommittedSceneIsTheSpecifiedScene()
        {
            Assert.That(BootSceneBuilder.DescribeOnDisk(), Is.EqualTo(BootSceneBuilder.Expected));
        }

        [Test]
        public void BootSceneIsTheOnlySceneInTheBuild()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.EqualTo(1));
            Assert.That(scenes[0].path, Is.EqualTo(BootSceneBuilder.BootScenePath));
            Assert.That(scenes[0].enabled, Is.True);
        }

        [Test]
        public void RunningTheBuilderAgainWritesNothing()
        {
            var before = new byte[Files.Length][];
            for (var i = 0; i < Files.Length; i++) before[i] = File.ReadAllBytes(Files[i]);

            Assert.That(BootSceneBuilder.Build(), Is.False, "the builder reported writing something");
            AssetDatabase.SaveAssets();

            for (var i = 0; i < Files.Length; i++)
                Assert.That(File.ReadAllBytes(Files[i]), Is.EqualTo(before[i]), $"{Files[i]} changed on a rerun");
        }
    }
}
