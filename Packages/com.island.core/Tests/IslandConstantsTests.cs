using NUnit.Framework;

namespace Island.Core.Tests
{
    /// <summary>
    /// These assert the derived constants against the figures the plan quotes
    /// (<c>docs/plan/02-ARCHITECTURE.md</c>, ADR-08). If someone changes the chunk size or the
    /// vertex spacing, the triangle and sample counts move with it and these tests say so,
    /// rather than the budget quietly drifting away from what the plan promised.
    /// </summary>
    [TestFixture]
    public class IslandConstantsTests
    {
        [Test]
        public void IslandIsSixtyFourMetresSquare()
        {
            Assert.That(IslandConstants.IslandExtentMetres, Is.EqualTo(64f));
            Assert.That(IslandConstants.IslandHalfExtentMetres, Is.EqualTo(32f));
            Assert.That(IslandConstants.IslandAreaSquareMetres, Is.EqualTo(4096f));
        }

        [Test]
        public void ChunkGridIsEightByEightOfEightMetres()
        {
            Assert.That(IslandConstants.ChunksPerSide, Is.EqualTo(8));
            Assert.That(IslandConstants.ChunkCount, Is.EqualTo(64));
            Assert.That(IslandConstants.ChunkSizeMetres, Is.EqualTo(8f));
        }

        [Test]
        public void ChunkHasThirtyThreeVerticesPerSideAtQuarterMetreSpacing()
        {
            Assert.That(IslandConstants.VertexSpacingMetres, Is.EqualTo(0.25f));
            Assert.That(IslandConstants.ChunkVerticesPerSide, Is.EqualTo(33));
            Assert.That(IslandConstants.ChunkVertexCount, Is.EqualTo(1089));
            Assert.That(IslandConstants.ChunkQuadsPerSide, Is.EqualTo(32));
        }

        [Test]
        public void TriangleCountsMatchTheBudget()
        {
            Assert.That(IslandConstants.ChunkTriangleCount, Is.EqualTo(2048));
            Assert.That(IslandConstants.IslandTriangleCount, Is.EqualTo(131072),
                "The plan budgets 131,072 triangles for the terrain on a Quest 2.");
        }

        [Test]
        public void HeightfieldIsOneSharedGridWithNoSeams()
        {
            // 257 = 8 chunks x 32 quads + 1. The +1 is the far border; without it the last
            // column of the island would have no sample and the edge chunks could not close.
            Assert.That(IslandConstants.HeightfieldResolution, Is.EqualTo(257));
            Assert.That(IslandConstants.HeightfieldSampleCount, Is.EqualTo(66049));
        }

        [Test]
        public void ChunkBordersShareTheirSamplesExactly()
        {
            // The point of one shared field: the last sample column of chunk n is the first
            // sample column of chunk n+1, the same index, not a copy. This is the arithmetic
            // that makes a cracked seam impossible.
            for (var chunk = 0; chunk < IslandConstants.ChunksPerSide - 1; chunk++)
            {
                var lastOfThisChunk = chunk * IslandConstants.ChunkQuadsPerSide
                                      + IslandConstants.ChunkQuadsPerSide;
                var firstOfNextChunk = (chunk + 1) * IslandConstants.ChunkQuadsPerSide;
                Assert.That(lastOfThisChunk, Is.EqualTo(firstOfNextChunk),
                    $"Chunks {chunk} and {chunk + 1} must read the same border sample.");
            }
        }

        [Test]
        public void TheLastSampleLandsExactlyOnTheIslandEdge()
        {
            var span = (IslandConstants.HeightfieldResolution - 1)
                       * IslandConstants.VertexSpacingMetres;
            Assert.That(span, Is.EqualTo(IslandConstants.IslandExtentMetres),
                "The heightfield must span the island exactly, with no gap or overhang.");
        }

        [Test]
        public void WaterSitsBelowTheGroundAndTheShoreReadsAboveIt()
        {
            Assert.That(IslandConstants.WaterLevelMetres, Is.EqualTo(-0.35f));
            Assert.That(IslandConstants.WaterLevelMetres, Is.LessThan(0f),
                "Flat ground is at 0 and must start dry.");
            Assert.That(IslandConstants.SandLineMetres,
                Is.GreaterThan(IslandConstants.WaterLevelMetres),
                "Wet sand has to show above the waterline or there is no visible shore.");
            Assert.That(IslandConstants.PlantSubmergedCutoffMetres,
                Is.LessThan(IslandConstants.WaterLevelMetres),
                "Plants must survive a nudge at the waterline rather than flicker.");
        }

        [Test]
        public void HeightLimitsAllowAPondAndAMountain()
        {
            Assert.That(IslandConstants.MinHeightMetres, Is.EqualTo(-3.5f));
            Assert.That(IslandConstants.MaxHeightMetres, Is.EqualTo(18f));
            Assert.That(IslandConstants.MinHeightMetres,
                Is.LessThan(IslandConstants.WaterLevelMetres),
                "It must be possible to dig below the waterline, or no pond can be made.");
        }

        [Test]
        public void ThereIsOneSurfaceWeightPerColourChannel()
        {
            Assert.That(IslandConstants.SurfaceChannelCount, Is.EqualTo(4));
            var channels = new[]
            {
                IslandConstants.SurfaceChannelSand,
                IslandConstants.SurfaceChannelGrass,
                IslandConstants.SurfaceChannelRock,
                IslandConstants.SurfaceChannelPath,
            };
            Assert.That(channels, Is.Unique);
            Assert.That(channels, Is.All.InRange(0, IslandConstants.SurfaceChannelCount - 1));
        }

        [Test]
        public void AutoRockUsesTheAngleTheCodeActuallyUsed()
        {
            // The blueprint's prose said 38 degrees and its code used 35. 35 is what anyone
            // looking at the terrain would have seen, so that is what the plan now states.
            Assert.That(IslandConstants.AutoRockSlopeDegrees, Is.EqualTo(35f));
        }

        [Test]
        public void TabletopShowsTheWholeIslandWithinArmsReach()
        {
            Assert.That(IslandConstants.TabletopScaleDivisor, Is.EqualTo(25f));
            var modelWidth = IslandConstants.IslandExtentMetres
                             / IslandConstants.TabletopScaleDivisor;
            Assert.That(modelWidth, Is.EqualTo(2.56f).Within(0.001f),
                "At 1:25 the island is a 2.56 m model — a big table, reachable by walking round it.");
        }

        [Test]
        public void IsSubmergedFollowsTheWaterLine()
        {
            Assert.That(IslandConstants.IsSubmerged(IslandConstants.WaterLevelMetres - 0.01f), Is.True);
            Assert.That(IslandConstants.IsSubmerged(IslandConstants.WaterLevelMetres), Is.True);
            Assert.That(IslandConstants.IsSubmerged(IslandConstants.WaterLevelMetres + 0.01f), Is.False);
            Assert.That(IslandConstants.IsSubmerged(0f), Is.False, "Flat ground starts dry.");
        }

        [Test]
        public void ClampHeightHoldsTheSculptableRange()
        {
            Assert.That(IslandConstants.ClampHeight(-99f), Is.EqualTo(IslandConstants.MinHeightMetres));
            Assert.That(IslandConstants.ClampHeight(99f), Is.EqualTo(IslandConstants.MaxHeightMetres));
            Assert.That(IslandConstants.ClampHeight(1.5f), Is.EqualTo(1.5f));
            Assert.That(IslandConstants.ClampHeight(IslandConstants.MinHeightMetres),
                Is.EqualTo(IslandConstants.MinHeightMetres));
            Assert.That(IslandConstants.ClampHeight(IslandConstants.MaxHeightMetres),
                Is.EqualTo(IslandConstants.MaxHeightMetres));
        }

        [Test]
        public void IsInsideIslandIncludesTheEdgeAndExcludesTheSea()
        {
            Assert.That(IslandConstants.IsInsideIsland(0f, 0f), Is.True);
            Assert.That(IslandConstants.IsInsideIsland(32f, 32f), Is.True, "The edge is on the island.");
            Assert.That(IslandConstants.IsInsideIsland(-32f, -32f), Is.True);
            Assert.That(IslandConstants.IsInsideIsland(32.01f, 0f), Is.False);
            Assert.That(IslandConstants.IsInsideIsland(0f, -32.01f), Is.False);
        }
    }
}
