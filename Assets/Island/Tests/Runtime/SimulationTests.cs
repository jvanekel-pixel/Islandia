using System;
using NUnit.Framework;
using UnityEngine;

namespace Island.Runtime.Tests
{
    /// <summary>The simulation parts on their own, without a scene.</summary>
    public class SimulationTests
    {
        [Test]
        public void ScriptedClockStartsAtZeroAndStepsExactly()
        {
            var clock = new ScriptedClock(0.5);
            Assert.That(clock.Frame, Is.EqualTo(0));
            Assert.That(clock.Seconds, Is.EqualTo(0));
            Assert.That(clock.DeltaSeconds, Is.EqualTo(0));

            for (var i = 0; i < 7; i++) clock.Advance();
            Assert.That(clock.Frame, Is.EqualTo(7));
            Assert.That(clock.Seconds, Is.EqualTo(3.5));
            Assert.That(clock.DeltaSeconds, Is.EqualTo(0.5f));
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void ScriptedClockRefusesANonsenseStep(double step)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptedClock(step));
        }

        [Test]
        public void ScriptedInputFollowsTheClockNotTheNumberOfReads()
        {
            var clock = new ScriptedClock();
            var input = new ScriptedInputSource(clock, c => new InputFrame { Right = new HandInput { Trigger = c.Frame } });

            clock.Advance();
            clock.Advance();
            Assert.That(input.Read().Right.Trigger, Is.EqualTo(2f));
            Assert.That(input.Read().Right.Trigger, Is.EqualTo(2f), "a second read in the same frame moved time");
        }

        [Test]
        public void IdlePlatformIsASimulationStandingStill()
        {
            var platform = SimulationPlatform.Idle();
            Assert.That(platform.IsSimulation, Is.True);
            Assert.That(platform.Device.IsHeadset, Is.False);

            var frame = platform.Input.Read();
            Assert.That(frame.HeadTracked, Is.True);
            Assert.That(frame.Head.position, Is.EqualTo(new Vector3(0f, 1.6f, 0f)));
        }

        [Test]
        public void PlatformRefusesMissingServices()
        {
            var clock = new ScriptedClock();
            var input = ScriptedInputSource.StandingStill(clock);
            Assert.Throws<ArgumentNullException>(() => SimulationPlatform.Create(null, clock));
            Assert.Throws<ArgumentNullException>(() => SimulationPlatform.Create(input, null));
        }
    }
}
