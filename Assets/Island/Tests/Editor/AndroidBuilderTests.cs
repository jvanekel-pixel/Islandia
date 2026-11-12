using System;
using Island.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Island.Editor.Tests
{
    /// <summary>F-07: what the Android build is asked for, checked without building.</summary>
    public class AndroidBuilderTests
    {
        [Test]
        public void BuildsTheBootSceneAsAnAndroidApk()
        {
            var options = AndroidBuilder.Options("Builds/x.apk");
            Assert.That(options.scenes, Is.EqualTo(new[] { BootSceneBuilder.BootScenePath }));
            Assert.That(options.target, Is.EqualTo(BuildTarget.Android));
            Assert.That(options.targetGroup, Is.EqualTo(BuildTargetGroup.Android));
            Assert.That(options.locationPathName, Is.EqualTo("Builds/x.apk"));
            Assert.That(options.options & BuildOptions.Development, Is.EqualTo(BuildOptions.None),
                "the headset gets a release build; a development build changes performance");
        }

        [Test]
        public void ReadsTheOutputFromTheCommandLine()
        {
            Assert.That(AndroidBuilder.OutputPath(new[] { "unity", "-islandOutput", "out/a.apk", "-batchmode" }),
                Is.EqualTo("out/a.apk"));
            Assert.That(AndroidBuilder.OutputPath(new[] { "unity", "-batchmode" }),
                Is.EqualTo(AndroidBuilder.DefaultOutput));
            Assert.Throws<ArgumentException>(() => AndroidBuilder.OutputPath(new[] { "unity", "-islandOutput" }));
            Assert.Throws<ArgumentException>(() => AndroidBuilder.OutputPath(new[] { "unity", "-islandOutput", "-batchmode" }));
        }

        [Test]
        public void RefusesToBuildFromAnotherTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                Assert.Ignore("the editor is already targeting Android, so there is nothing to refuse");
            Assert.Throws<UnityEditor.Build.BuildFailedException>(() => AndroidBuilder.Build("Builds/never.apk"));
        }
    }
}
