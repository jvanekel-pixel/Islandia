using System;
using Island.Core.Rides;
using Island.Core.Terrain;
using NUnit.Framework;

namespace Island.Core.Tests.Rides
{
    /// <summary>
    /// A coaster's grid is the island's world space in sub-units. These pin the mapping to
    /// metres and to heightfield samples, and the integer ground the validator reads.
    /// </summary>
    [TestFixture]
    public class CoasterSpaceTests
    {
        [Test]
        public void TheConstantsAgreeWithThePackAndTheIsland()
        {
            Assert.That(CoasterSpace.SubUnitsPerMetre * CoasterPieces.MetresPerUnit, Is.EqualTo(CoasterShapes.SubUnitsPerUnit));
            Assert.That(CoasterSpace.SubUnitsPerSample, Is.EqualTo(CoasterSpace.SubUnitsPerMetre * IslandConstants.VertexSpacingMetres));
            Assert.That(CoasterSpace.IslandHalfExtent, Is.EqualTo(CoasterSpace.SubUnitsPerMetre * IslandConstants.IslandHalfExtentMetres));
            Assert.That(CoasterSpace.MetresOf(1), Is.EqualTo(CoasterPieces.MetresPerSubUnit));
        }

        [Test]
        public void ATrackPointIsWhereTheHeightfieldSampleIs()
        {
            // Sample (130, 140) is 2 and 12 samples east and north of the centre: 0.5 m and 3 m.
            var point = new TrackPoint(130 * 8 - 1024, 48, 140 * 8 - 1024);
            Assert.That(point, Is.EqualTo(new TrackPoint(16, 48, 96)));
            var (x, y, z) = CoasterSpace.WorldOf(point);
            Assert.That(x, Is.EqualTo(Heightfield.WorldXOf(130)));
            Assert.That(z, Is.EqualTo(Heightfield.WorldZOf(140)));
            Assert.That(x, Is.EqualTo(0.5f));
            Assert.That(y, Is.EqualTo(1.5f));
            Assert.That(z, Is.EqualTo(3f));
        }

        [Test]
        public void TheGroundIsReadAtTheRightSampleNotItsTranspose()
        {
            var field = new Heightfield();
            field.SetHeight(130, 140, 1.5f);
            var ground = new HeightfieldGround(field);

            Assert.That(ground.TryGetGround(16, 96, out var at), Is.True);
            Assert.That(at, Is.EqualTo(48));
            Assert.That(ground.TryGetGround(96, 16, out var transposed), Is.True);
            Assert.That(transposed, Is.EqualTo(0));
        }

        [Test]
        public void BetweenSamplesTheGroundIsInterpolatedExactlyAndRoundedUp()
        {
            var field = new Heightfield();
            field.SetHeight(130, 140, 1.5f); // 48 sub-units
            var ground = new HeightfieldGround(field);

            // Three eighths of the way east to the next sample: 48 * 5/8 = 30.
            ground.TryGetGround(16 + 3, 96, out var east);
            Assert.That(east, Is.EqualTo(30));
            // Half way east and a quarter north: 48 * 1/2 * 3/4 = 18.
            ground.TryGetGround(16 + 4, 96 + 2, out var both);
            Assert.That(both, Is.EqualTo(18));
            // One eighth of the way: 48 * 7/8 = 42; one eighth of 1 m (32) is 4 → 28.
            field.SetHeight(130, 140, 1f);
            ground.TryGetGround(16 + 1, 96, out var rounded);
            Assert.That(rounded, Is.EqualTo(28));
            // A height that is not a whole sub-unit rounds up: 1/1024 m above 0.5 m is 17, not 16.
            field.SetHeight(130, 140, 0.5f + 1f / 1024f);
            ground.TryGetGround(16, 96, out var up);
            Assert.That(up, Is.EqualTo(17));
            // And below zero, up is towards zero: −0.5 m − 1/1024 m is −16.
            field.SetHeight(130, 140, -0.5f - 1f / 1024f);
            ground.TryGetGround(16, 96, out var down);
            Assert.That(down, Is.EqualTo(-16));
        }

        [Test]
        public void ThereIsGroundToTheEdgeAndNoneBeyondIt()
        {
            var field = new Heightfield();
            field.SetHeight(256, 0, 2f);
            var ground = new HeightfieldGround(field);

            Assert.That(ground.TryGetGround(1024, -1024, out var corner), Is.True);
            Assert.That(corner, Is.EqualTo(64));
            Assert.That(ground.TryGetGround(-1024, 1024, out _), Is.True);
            Assert.That(ground.TryGetGround(1025, 0, out _), Is.False);
            Assert.That(ground.TryGetGround(0, -1025, out _), Is.False);
            Assert.That(CoasterSpace.IsOverIsland(-1025, 0), Is.False);
        }

        [Test]
        public void FlatGroundIsTheSameEverywhere()
        {
            var ground = new FlatGround(-7);
            Assert.That(ground.TryGetGround(int.MaxValue, int.MinValue, out var height), Is.True);
            Assert.That(height, Is.EqualTo(-7));
            Assert.Throws<ArgumentNullException>(() => new HeightfieldGround(null));
        }
    }
}
