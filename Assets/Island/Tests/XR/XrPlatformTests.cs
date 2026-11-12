using Island.Runtime;
using NUnit.Framework;

namespace Island.XR.Tests
{
    /// <summary>
    /// The headset platform stands aside when there is no headset, which is what lets the editor
    /// and every PlayMode test boot the simulation (ADR-07).
    /// </summary>
    public class XrPlatformTests
    {
        [Test]
        public void OffersNoHeadsetWhenNoXrLoaderIsRunning()
        {
            Assert.That(XrPlatform.TryCreate(), Is.Null,
                "the editor has no XR loader running (Standalone has none, F-05), so there is no headset to offer");
        }

        [Test]
        public void ReadingWithNoDevicesReportsNothingTracked()
        {
            InputFrame frame = new XrInputSource().Read();
            Assert.That(frame.HeadTracked, Is.False);
            Assert.That(frame.Left.Tracked, Is.False);
            Assert.That(frame.Right.Tracked, Is.False);
        }
    }
}
