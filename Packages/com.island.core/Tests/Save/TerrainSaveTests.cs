using System;
using Island.Core.Save;
using Island.Core.Terrain;
using NUnit.Framework;

namespace Island.Core.Tests.Save
{
    /// <summary>
    /// Does an island come back the way it was left?
    /// </summary>
    [TestFixture]
    public class TerrainSaveTests
    {
        /// <summary>An island with hills, a pond, a cliff and four surfaces painted on it.</summary>
        private static Heightfield SculptedIsland()
        {
            var field = new Heightfield();
            TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Raise, -8f, -8f, 6f, 4f), 1f);
            TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Raise, 10f, 4f, 3f, 9f), 1f);
            TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Lower, 6f, -10f, 4f, 3f), 1f);
            TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Paint, -8f, -8f, 5f, 8f,
                surfaceChannel: IslandConstants.SurfaceChannelGrass), 1f);
            TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Paint, 0f, 0f, 3f, 8f,
                surfaceChannel: IslandConstants.SurfaceChannelPath), 1f);
            TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Paint, 10f, 4f, 2f, 8f,
                surfaceChannel: IslandConstants.SurfaceChannelRock), 1f);
            return field;
        }

        private static Heightfield RoundTrip(Heightfield original)
        {
            var container = new SaveContainer();
            container.Put(new TerrainSaveSection(original));

            var reloaded = new Heightfield();
            SaveContainer.FromBytes(container.ToBytes()).ReadInto(new TerrainSaveSection(reloaded));
            return reloaded;
        }

        [Test]
        public void TheGroundComesBackWithinTheQuantisationStep()
        {
            var original = SculptedIsland();
            var reloaded = RoundTrip(original);

            var step = TerrainSaveSection.QuantisationStepMetres;
            var worst = 0f;
            for (var z = 0; z < original.SampleResolution; z++)
            for (var x = 0; x < original.SampleResolution; x++)
            {
                var difference = Math.Abs(original.HeightAt(x, z) - reloaded.HeightAt(x, z));
                if (difference > worst) worst = difference;
            }

            Assert.That(worst, Is.LessThanOrEqualTo(step),
                $"the worst sample moved by {worst} m, more than the {step} m the format promises");
        }

        [Test]
        public void TheStepIsFarTooSmallToSee()
        {
            // Samples are 25 cm apart. If the step were anywhere near visible, storing heights as
            // 16-bit steps would be the wrong trade.
            Assert.That(TerrainSaveSection.QuantisationStepMetres, Is.LessThan(0.001f));
            Assert.That(TerrainSaveSection.QuantisationStepMetres,
                Is.LessThan(IslandConstants.VertexSpacingMetres / 100f));
        }

        [Test]
        public void EverySurfaceWeightComesBackExactly()
        {
            // Weights are already bytes, so there is no excuse for losing any of them.
            var original = SculptedIsland();
            var reloaded = RoundTrip(original);

            for (var z = 0; z < original.SampleResolution; z++)
            for (var x = 0; x < original.SampleResolution; x++)
            for (var channel = 0; channel < IslandConstants.SurfaceChannelCount; channel++)
            {
                Assert.That(reloaded.WeightAt(x, z, channel), Is.EqualTo(original.WeightAt(x, z, channel)),
                    $"channel {channel} at ({x}, {z})");
            }
        }

        [Test]
        public void WeightsStillSumTo255AfterALoad()
        {
            var reloaded = RoundTrip(SculptedIsland());
            for (var z = 0; z < reloaded.SampleResolution; z += 7)
            for (var x = 0; x < reloaded.SampleResolution; x += 7)
                Assert.That(reloaded.WeightSumAt(x, z), Is.EqualTo(255), $"at ({x}, {z})");
        }

        [Test]
        public void SavingAReloadedIslandProducesTheSameBytes()
        {
            // Load and save with nothing in between, and the file should not drift. Without this
            // an island could creep a fraction lower every time it was opened.
            var container = new SaveContainer(0L);
            container.Put(new TerrainSaveSection(SculptedIsland()));
            var first = container.ToBytes();

            var reloaded = new Heightfield();
            SaveContainer.FromBytes(first).ReadInto(new TerrainSaveSection(reloaded));

            var again = new SaveContainer(0L);
            again.Put(new TerrainSaveSection(reloaded));

            Assert.That(again.ToBytes(), Is.EqualTo(first), "a save and reload cycle must be stable");
        }

        [Test]
        public void TheFlatStartingIslandRoundTrips()
        {
            var reloaded = RoundTrip(new Heightfield());
            Assert.That(reloaded.HeightAt(128, 128), Is.EqualTo(0f).Within(TerrainSaveSection.QuantisationStepMetres));
            Assert.That(reloaded.WeightAt(128, 128, IslandConstants.SurfaceChannelSand), Is.EqualTo(255));
        }

        [Test]
        public void TheExtremesOfTheRangeSurvive()
        {
            var original = new Heightfield();
            original.SetHeight(0, 0, IslandConstants.MinHeightMetres);
            original.SetHeight(256, 256, IslandConstants.MaxHeightMetres);
            original.SetHeight(128, 128, IslandConstants.WaterLevelMetres);

            var reloaded = RoundTrip(original);
            var step = TerrainSaveSection.QuantisationStepMetres;
            Assert.That(reloaded.HeightAt(0, 0), Is.EqualTo(IslandConstants.MinHeightMetres).Within(step));
            Assert.That(reloaded.HeightAt(256, 256), Is.EqualTo(IslandConstants.MaxHeightMetres).Within(step));
            Assert.That(reloaded.HeightAt(128, 128), Is.EqualTo(IslandConstants.WaterLevelMetres).Within(step));
        }

        [Test]
        public void ThePondIsStillUnderWaterAfterALoad()
        {
            // Quantisation must not nudge a dug pond back above the waterline; a pond that
            // drained itself on load would be a bug nobody could explain.
            var original = SculptedIsland();
            var reloaded = RoundTrip(original);

            for (var z = 0; z < original.SampleResolution; z++)
            for (var x = 0; x < original.SampleResolution; x++)
            {
                if (!IslandConstants.IsSubmerged(original.HeightAt(x, z))) continue;
                Assert.That(reloaded.HeightAt(x, z),
                    Is.LessThanOrEqualTo(IslandConstants.WaterLevelMetres + TerrainSaveSection.QuantisationStepMetres),
                    $"({x}, {z}) was under water and came back above it");
            }
        }

        [Test]
        public void AnIslandSavedAtAnotherSizeIsRefusedClearly()
        {
            var payload = new TerrainSaveSection(new Heightfield()).Write();
            BitConverter.GetBytes(129).CopyTo(payload, 0);   // a smaller resolution

            var error = Assert.Throws<SaveFormatException>(
                () => new TerrainSaveSection(new Heightfield()).Read(payload, TerrainSaveSection.Version));
            Assert.That(error.Message, Does.Contain("129").And.Contain("257"));
        }

        [Test]
        public void ASectionFromAFutureVersionIsNotReadAsThoughItWereCurrent()
        {
            var payload = new TerrainSaveSection(new Heightfield()).Write();
            var error = Assert.Throws<SaveFormatException>(
                () => new TerrainSaveSection(new Heightfield()).Read(payload, TerrainSaveSection.Version + 1));
            Assert.That(error.Message, Does.Contain("migrations"));
        }

        [Test]
        public void ATruncatedTerrainSectionSaysSoRatherThanLeavingHalfAnIsland()
        {
            var payload = new TerrainSaveSection(new Heightfield()).Write();
            Array.Resize(ref payload, payload.Length / 2);
            Assert.Throws<SaveFormatException>(
                () => new TerrainSaveSection(new Heightfield()).Read(payload, TerrainSaveSection.Version));
        }

        [Test]
        public void AnIslandSurvivesTheWholeRoundTripThroughAFile()
        {
            // The real path end to end: sculpt, save to the store, load it back.
            var store = new FakeFileStore();
            var file = new SaveFile(store);
            var original = SculptedIsland();

            var container = new SaveContainer();
            container.Put(new TerrainSaveSection(original));
            file.Save("/islands/slot1.island", container);

            var result = file.Load("/islands/slot1.island");
            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.Loaded));

            var reloaded = new Heightfield();
            Assert.That(result.Container.ReadInto(new TerrainSaveSection(reloaded)), Is.True);
            Assert.That(reloaded.HeightAt(96, 96),
                Is.EqualTo(original.HeightAt(96, 96)).Within(TerrainSaveSection.QuantisationStepMetres));
        }

        [Test]
        public void TheWholeIslandFitsInASensibleAmountOfSpace()
        {
            // 66,049 samples. As floats with four weight bytes it would be 528 KB before packing;
            // this should be a fraction of that, because a save that takes a visible moment to
            // write is a save people stop making.
            var container = new SaveContainer();
            container.Put(new TerrainSaveSection(SculptedIsland()));
            var bytes = container.ToBytes();

            Assert.That(bytes.Length, Is.LessThan(400_000),
                $"the terrain section came to {bytes.Length} bytes");
        }
    }
}
