using System;
using System.Collections.Generic;
using System.Linq;
using Island.Core.Terrain;
using NUnit.Framework;

namespace Island.Core.Tests.Terrain
{
    /// <summary>
    /// Sculpting is the part of this game most likely to feel wrong, and the part hardest to
    /// judge from a screenshot. It is pure logic on purpose, so it can be held to properties
    /// rather than eyeballed in a headset.
    /// </summary>
    [TestFixture]
    public class TerrainSculptorTests
    {
        private static TerrainBrush Raise(float x, float z, float radius = 2f, float strength = 1f) =>
            new TerrainBrush(BrushKind.Raise, x, z, radius, strength);

        private static float Peak(Heightfield field) =>
            Enumerable.Range(0, field.SampleResolution * field.SampleResolution)
                .Select(i => field.HeightAt(i % field.SampleResolution, i / field.SampleResolution))
                .Max();

        // ---- the brushes do what they say ---------------------------------------------------------

        [Test]
        public void ANewIslandIsFlatDrySand()
        {
            var field = new Heightfield();
            Assert.That(field.HeightAt(128, 128), Is.EqualTo(0f));
            Assert.That(field.WeightAt(128, 128, IslandConstants.SurfaceChannelSand), Is.EqualTo(255));
            Assert.That(field.WeightAt(128, 128, IslandConstants.SurfaceChannelGrass), Is.EqualTo(0));
            Assert.That(IslandConstants.IsSubmerged(0f), Is.False);
        }

        [Test]
        public void RaiseBuildsAHillHighestAtTheCentre()
        {
            var field = new Heightfield();
            TerrainSculptor.Apply(field, Raise(0f, 0f, radius: 4f, strength: 2f), 1f);

            var centre = field.HeightAt(128, 128);
            Assert.That(centre, Is.GreaterThan(0f));
            Assert.That(field.HeightAt(132, 128), Is.LessThan(centre), "it should fall away from the middle");
            Assert.That(field.HeightAt(144, 128), Is.EqualTo(0f), "and reach nothing outside the brush");
        }

        [Test]
        public void LowerDigsBelowTheWaterLine()
        {
            var field = new Heightfield();
            TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Lower, 0f, 0f, 3f, 2f), 1f);

            var centre = field.HeightAt(128, 128);
            Assert.That(centre, Is.LessThan(IslandConstants.WaterLevelMetres),
                "digging has to be able to make a pond, or there is no pond tool");
            Assert.That(IslandConstants.IsSubmerged(centre), Is.True);
        }

        [Test]
        public void FlattenPullsRoughGroundTowardsOneHeight()
        {
            var field = new Heightfield();
            TerrainSculptor.Apply(field, Raise(0f, 0f, 4f, 3f), 1f);
            TerrainSculptor.Apply(field, Raise(1f, 1f, 2f, 2f), 1f);

            var target = 1.5f;
            for (var step = 0; step < 60; step++)
            {
                TerrainSculptor.Apply(field,
                    new TerrainBrush(BrushKind.Flatten, 0f, 0f, 4f, 6f, flattenTargetMetres: target), 1f / 60f);
            }

            Assert.That(field.HeightAt(128, 128), Is.EqualTo(target).Within(0.02f));
        }

        [Test]
        public void SmoothTakesTheEdgeOffASpike()
        {
            var field = new Heightfield();
            field.SetHeight(128, 128, 5f);
            var before = Roughness(field, 126, 126, 130, 130);

            for (var step = 0; step < 30; step++)
            {
                TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Smooth, 0f, 0f, 2f, 8f), 1f / 60f);
            }

            Assert.That(Roughness(field, 126, 126, 130, 130), Is.LessThan(before * 0.5f));
            Assert.That(field.HeightAt(128, 128), Is.LessThan(5f));
        }

        [Test]
        public void SmoothDoesNotSmearTowardsWhereTheLoopStarted()
        {
            // Reading and writing the same array would drag the ground towards the corner the
            // loop happens to begin at. A symmetric spike must stay symmetric.
            var field = new Heightfield();
            field.SetHeight(128, 128, 4f);
            for (var step = 0; step < 20; step++)
            {
                TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Smooth, 0f, 0f, 3f, 6f), 1f / 60f);
            }

            Assert.That(field.HeightAt(126, 128), Is.EqualTo(field.HeightAt(130, 128)).Within(1e-5f));
            Assert.That(field.HeightAt(128, 126), Is.EqualTo(field.HeightAt(128, 130)).Within(1e-5f));
        }

        [Test]
        public void PaintLaysDownASurfaceWithoutMovingTheGround()
        {
            var field = new Heightfield();
            var heightBefore = field.HeightAt(128, 128);

            for (var step = 0; step < 60; step++)
            {
                TerrainSculptor.Apply(field,
                    new TerrainBrush(BrushKind.Paint, 0f, 0f, 3f, 6f,
                                     surfaceChannel: IslandConstants.SurfaceChannelGrass), 1f / 60f);
            }

            Assert.That(field.WeightAt(128, 128, IslandConstants.SurfaceChannelGrass), Is.GreaterThan(200));
            Assert.That(field.WeightAt(128, 128, IslandConstants.SurfaceChannelSand), Is.LessThan(55));
            Assert.That(field.HeightAt(128, 128), Is.EqualTo(heightBefore), "painting must not move the ground");
        }

        // ---- properties ----------------------------------------------------------------------------

        [Test]
        public void AStrokeIsTheSameShapeAtThirtySeventyTwoAndNinetyHertz()
        {
            // The two headsets this must run on differ in exactly this, so a stroke that carves
            // deeper on the faster one is a real defect, not a rounding curiosity.
            foreach (var kind in new[] { BrushKind.Raise, BrushKind.Lower })
            {
                var results = new List<float>();
                foreach (var hz in new[] { 30, 72, 90 })
                {
                    var field = new Heightfield();
                    for (var step = 0; step < hz; step++)
                    {
                        TerrainSculptor.Apply(field, new TerrainBrush(kind, 0f, 0f, 3f, 1.5f), 1f / hz);
                    }
                    results.Add(field.HeightAt(128, 128));
                }
                Assert.That(results[1], Is.EqualTo(results[0]).Within(0.001f), $"{kind} at 72 vs 30 Hz");
                Assert.That(results[2], Is.EqualTo(results[0]).Within(0.001f), $"{kind} at 90 vs 30 Hz");
            }
        }

        [Test]
        public void FlattenAndSmoothAreAlsoTheSameAtAnyFrameRate()
        {
            // These approach a target rather than adding to it. A plain linear blend would leave
            // (1-a)^n of the distance, which is not what one longer step leaves, so the pad would
            // come out flatter on a faster machine. The exponential form composes exactly.
            var results = new List<float>();
            foreach (var hz in new[] { 30, 72, 90 })
            {
                var field = new Heightfield();
                TerrainSculptor.Apply(field, Raise(0f, 0f, 4f, 4f), 1f);
                for (var step = 0; step < hz; step++)
                {
                    TerrainSculptor.Apply(field,
                        new TerrainBrush(BrushKind.Flatten, 0f, 0f, 4f, 3f, flattenTargetMetres: 0.5f), 1f / hz);
                }
                results.Add(field.HeightAt(128, 128));
            }
            Assert.That(results[1], Is.EqualTo(results[0]).Within(0.002f), "flatten at 72 vs 30 Hz");
            Assert.That(results[2], Is.EqualTo(results[0]).Within(0.002f), "flatten at 90 vs 30 Hz");
        }

        [Test]
        public void FlattenIsIdempotentOnGroundAlreadyAtTheTarget()
        {
            // The real idempotence property. Note it is about ground that has *arrived*, not
            // about a brush that has been held for a while: at the rim the falloff approaches
            // zero, so those samples are still genuinely creeping towards the target long after
            // the middle has settled. An earlier version of this test asserted the whole stroke
            // went quiet and was simply wrong about the shape of the falloff.
            var field = new Heightfield();
            const float target = 1f;
            for (var z = 100; z <= 156; z++)
            for (var x = 100; x <= 156; x++)
                field.SetHeight(x, z, target);

            var flatten = new TerrainBrush(BrushKind.Flatten, 0f, 0f, 4f, 20f, flattenTargetMetres: target);
            var first = TerrainSculptor.Apply(field, flatten, 1f / 60f);

            Assert.That(first.IsEmpty, Is.True,
                "flattening ground that is already flat must not dirty a single chunk — holding " +
                "the trigger over a finished pad would otherwise rebuild meshes every frame");
            Assert.That(field.HeightAt(128, 128), Is.EqualTo(target));
        }

        [Test]
        public void TheMiddleOfAFlattenStrokeSettlesAndStaysPut()
        {
            var field = new Heightfield();
            TerrainSculptor.Apply(field, Raise(0f, 0f, 4f, 3f), 1f);
            var flatten = new TerrainBrush(BrushKind.Flatten, 0f, 0f, 4f, 20f, flattenTargetMetres: 1f);

            for (var step = 0; step < 200; step++) TerrainSculptor.Apply(field, flatten, 1f / 60f);
            var settled = field.HeightAt(128, 128);

            for (var step = 0; step < 60; step++) TerrainSculptor.Apply(field, flatten, 1f / 60f);
            Assert.That(field.HeightAt(128, 128), Is.EqualTo(settled).Within(1e-6f));
            Assert.That(settled, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void AChangeTooSmallToSeeIsNotWorthRebuildingAMeshFor()
        {
            // Sub-epsilon drift must not keep chunks dirty: samples are 25 cm apart, so a
            // hundredth of a millimetre cannot show, and redrawing for it costs real frame time
            // on the headset this has to run on.
            var field = new Heightfield();
            var nudge = new TerrainBrush(BrushKind.Raise, 0f, 0f, 2f,
                                         TerrainSculptor.HeightEpsilonMetres * 0.01f);
            Assert.That(TerrainSculptor.Apply(field, nudge, 1f / 60f).IsEmpty, Is.True);
        }

        [Test]
        public void ABrushIsRadiallySymmetricAboutItsCentre()
        {
            var field = new Heightfield();
            TerrainSculptor.Apply(field, Raise(0f, 0f, 4f, 2f), 1f);

            for (var offset = 1; offset <= 12; offset++)
            {
                var east = field.HeightAt(128 + offset, 128);
                Assert.That(field.HeightAt(128 - offset, 128), Is.EqualTo(east).Within(1e-5f), $"west at {offset}");
                Assert.That(field.HeightAt(128, 128 + offset), Is.EqualTo(east).Within(1e-5f), $"north at {offset}");
                Assert.That(field.HeightAt(128, 128 - offset), Is.EqualTo(east).Within(1e-5f), $"south at {offset}");
            }
        }

        [Test]
        public void HeightsNeverLeaveTheSculptableRange()
        {
            var field = new Heightfield();
            for (var step = 0; step < 400; step++)
                TerrainSculptor.Apply(field, Raise(0f, 0f, 4f, 50f), 1f / 10f);
            Assert.That(Peak(field), Is.EqualTo(IslandConstants.MaxHeightMetres).Within(1e-4f));

            for (var step = 0; step < 800; step++)
                TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Lower, 0f, 0f, 4f, 50f), 1f / 10f);
            Assert.That(field.HeightAt(128, 128), Is.EqualTo(IslandConstants.MinHeightMetres).Within(1e-4f));
        }

        [Test]
        public void SurfaceWeightsAlwaysSumToTheFull255()
        {
            var field = new Heightfield();
            var random = new Random(20261110);
            for (var stroke = 0; stroke < 60; stroke++)
            {
                var channel = random.Next(IslandConstants.SurfaceChannelCount);
                TerrainSculptor.Apply(field,
                    new TerrainBrush(BrushKind.Paint,
                        (float)(random.NextDouble() * 8 - 4), (float)(random.NextDouble() * 8 - 4),
                        (float)(random.NextDouble() * 3 + 1), (float)(random.NextDouble() * 6 + 1),
                        surfaceChannel: channel),
                    1f / 60f);
            }

            for (var z = 110; z <= 146; z++)
            for (var x = 110; x <= 146; x++)
                Assert.That(field.WeightSumAt(x, z), Is.EqualTo(255), $"weights drifted at ({x}, {z})");
        }

        [Test]
        public void StrokesAtEveryCornerAndEdgeStayOnTheIsland()
        {
            var half = IslandConstants.IslandHalfExtentMetres;
            foreach (var (x, z) in new[]
                     {
                         (-half, -half), (half, half), (-half, half), (half, -half),
                         (0f, half), (half, 0f), (-half - 20f, 0f), (0f, half + 20f),
                     })
            {
                var field = new Heightfield();
                Assert.DoesNotThrow(() => TerrainSculptor.Apply(field, Raise(x, z, 6f, 2f), 1f),
                    $"a stroke at ({x}, {z}) should clamp, not fall off the edge");
            }
        }

        [Test]
        public void ZeroOrNegativeElapsedTimeChangesNothing()
        {
            var field = new Heightfield();
            Assert.That(TerrainSculptor.Apply(field, Raise(0f, 0f), 0f).IsEmpty, Is.True);
            Assert.That(TerrainSculptor.Apply(field, Raise(0f, 0f), -1f).IsEmpty, Is.True);
            Assert.That(field.HeightAt(128, 128), Is.EqualTo(0f));
        }

        // ---- slope and rock --------------------------------------------------------------------------

        [Test]
        public void FlatGroundIsNotRock()
        {
            var field = new Heightfield();
            Assert.That(field.SlopeDegreesAt(128, 128), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(field.IsRockBySlope(128, 128), Is.False);
        }

        [Test]
        public void ACliffFaceBecomesRockAtTheOneThreshold()
        {
            var field = new Heightfield();
            // A step of one vertex spacing over one spacing is 45 degrees, comfortably past the
            // threshold; the point is that the threshold comes from IslandConstants, not a literal.
            field.SetHeight(129, 128, IslandConstants.VertexSpacingMetres * 2f);
            Assert.That(field.SlopeDegreesAt(128, 128),
                Is.GreaterThan(IslandConstants.AutoRockSlopeDegrees));
            Assert.That(field.IsRockBySlope(128, 128), Is.True);
        }

        [Test]
        public void AGentleSlopeIsNotRock()
        {
            var field = new Heightfield();
            for (var x = 120; x <= 136; x++) field.SetHeight(x, 128, (x - 120) * 0.02f);
            Assert.That(field.SlopeDegreesAt(128, 128),
                Is.LessThan(IslandConstants.AutoRockSlopeDegrees));
            Assert.That(field.IsRockBySlope(128, 128), Is.False);
        }

        // ---- helpers ----------------------------------------------------------------------------------

        private static float Roughness(Heightfield field, int minX, int minZ, int maxX, int maxZ)
        {
            var total = 0f;
            for (var z = minZ; z <= maxZ; z++)
            for (var x = minX; x <= maxX; x++)
            {
                total += Math.Abs(field.HeightAt(x, z) - field.HeightAt(x + 1, z));
                total += Math.Abs(field.HeightAt(x, z) - field.HeightAt(x, z + 1));
            }
            return total;
        }
    }
}
