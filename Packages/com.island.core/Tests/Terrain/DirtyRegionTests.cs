using System;
using System.Collections.Generic;
using System.Linq;
using Island.Core.Terrain;
using NUnit.Framework;

namespace Island.Core.Tests.Terrain
{
    /// <summary>
    /// Which chunks a stroke forces a rebuild of.
    /// </summary>
    /// <remarks>
    /// Too wide and every stroke rebuilds the island, which is frame time the Quest 2 does not
    /// have. Too narrow and a chunk keeps a stale edge, which is a crack in the ground you can
    /// see through. The tests here compare the answer against brute force — actually sculpting
    /// and looking at which samples moved — rather than against the same arithmetic restated.
    /// </remarks>
    [TestFixture]
    public class DirtyRegionTests
    {
        /// <summary>Every chunk that owns a sample which actually changed.</summary>
        private static HashSet<(int, int)> ChunksThatReallyChanged(float[] before, Heightfield after)
        {
            var found = new HashSet<(int, int)>();
            var resolution = after.SampleResolution;
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                if (before[z * resolution + x] == after.HeightAt(x, z)) continue;
                // A sample on a border belongs to the chunks on both sides of it.
                foreach (var cx in ChunksOwning(x))
                foreach (var cz in ChunksOwning(z))
                    found.Add((cx, cz));
            }
            return found;
        }

        private static IEnumerable<int> ChunksOwning(int sample)
        {
            const int quads = IslandConstants.ChunkQuadsPerSide;
            const int last = IslandConstants.ChunksPerSide - 1;
            for (var chunk = 0; chunk <= last; chunk++)
            {
                if (sample >= chunk * quads && sample <= chunk * quads + quads) yield return chunk;
            }
        }

        [Test]
        public void ASampleOnAChunkBorderBelongsToBothChunks()
        {
            // 32 is the seam between chunk 0 and chunk 1. Missing one of them leaves that chunk
            // drawing the old height along its edge.
            var (from, to) = DirtyRegion.ChunkSpan(32, 32);
            Assert.That(from, Is.EqualTo(0));
            Assert.That(to, Is.EqualTo(1));
        }

        [Test]
        public void ASampleInsideAChunkBelongsOnlyToIt()
        {
            var (from, to) = DirtyRegion.ChunkSpan(33, 33);
            Assert.That(from, Is.EqualTo(1));
            Assert.That(to, Is.EqualTo(1));
        }

        [Test]
        public void TheFirstAndLastSamplesStayInRange()
        {
            var first = DirtyRegion.ChunkSpan(0, 0);
            Assert.That(first, Is.EqualTo((0, 0)));

            var last = DirtyRegion.ChunkSpan(IslandConstants.HeightfieldResolution - 1,
                                             IslandConstants.HeightfieldResolution - 1);
            Assert.That(last, Is.EqualTo((IslandConstants.ChunksPerSide - 1,
                                          IslandConstants.ChunksPerSide - 1)));
        }

        [Test]
        public void EveryRadiusReportsExactlyTheChunksThatChanged()
        {
            // The headline property, over the whole range of brush sizes the tools offer and a
            // spread of positions including seams, the middle of chunks, and the island's edge.
            var positions = new[] { 0f, 0.125f, 4f, -4f, 8f, 16.0f, 31.9f, -31.9f, 23.5f };
            for (var radius = TerrainBrush.MinRadiusMetres;
                 radius <= TerrainBrush.MaxRadiusMetres;
                 radius += 0.25f)
            {
                foreach (var position in positions)
                {
                    var field = new Heightfield();
                    var before = field.CopyHeights();
                    var region = TerrainSculptor.Apply(field,
                        new TerrainBrush(BrushKind.Raise, position, position, radius, 2f), 1f / 60f);

                    var expected = ChunksThatReallyChanged(before, field);
                    var reported = new HashSet<(int, int)>(region.Chunks());

                    Assert.That(reported, Is.EquivalentTo(expected),
                        $"radius {radius} at ({position}, {position}): " +
                        $"reported {reported.Count}, actually changed {expected.Count}");
                    Assert.That(region.ChunkCount, Is.EqualTo(reported.Count));
                }
            }
        }

        [Test]
        public void ASmallBrushInTheMiddleOfAChunkTouchesOnlyThatChunk()
        {
            // Chunk 4 spans samples 128..160, so its middle is comfortably clear of both seams.
            var middleOfChunk = Heightfield.WorldXOf(144);
            var field = new Heightfield();
            var region = TerrainSculptor.Apply(field,
                new TerrainBrush(BrushKind.Raise, middleOfChunk, middleOfChunk, 1f, 2f), 1f / 60f);

            Assert.That(region.ChunkCount, Is.EqualTo(1),
                "a small stroke well inside one chunk should not cost three more rebuilds");
        }

        [Test]
        public void TheLargestBrushStaysWellShortOfTheWholeIsland()
        {
            var field = new Heightfield();
            var region = TerrainSculptor.Apply(field,
                new TerrainBrush(BrushKind.Raise, 0f, 0f, TerrainBrush.MaxRadiusMetres, 2f), 1f / 60f);

            Assert.That(region.ChunkCount, Is.LessThanOrEqualTo(9),
                "even the biggest brush should be a local cost, not an island-wide one");
            Assert.That(region.ChunkCount, Is.LessThan(IslandConstants.ChunkCount));
        }

        [Test]
        public void ARoundBrushDoesNotRebuildTheCornersOfItsBoundingBox()
        {
            // The reason chunks are a bitmask rather than a rectangle. A circular stroke centred
            // on a chunk corner spans three chunks each way, but does not reach the four diagonal
            // corners of that 3x3 block — so a rectangle would order nine mesh rebuilds where
            // five are needed, on the stroke people hold down the longest.
            var field = new Heightfield();
            var region = TerrainSculptor.Apply(field,
                new TerrainBrush(BrushKind.Raise, 4f, 4f, 4.25f, 2f), 1f / 60f);

            var boundingBox = DirtyRegion.FromSamples(region.MinX, region.MinZ, region.MaxX, region.MaxZ);
            Assert.That(region.ChunkCount, Is.LessThan(boundingBox.ChunkCount),
                "the circle should cost less than the box around it");
            Assert.That(region.ChunkCount, Is.EqualTo(5));
            Assert.That(boundingBox.ChunkCount, Is.EqualTo(9));
        }

        [Test]
        public void NothingChangedMeansNothingToRebuild()
        {
            var field = new Heightfield();
            var region = TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Raise, 0f, 0f, 2f, 0f), 1f / 60f);
            Assert.That(region.IsEmpty, Is.True);
            Assert.That(region.ChunkCount, Is.EqualTo(0));
            Assert.That(region.Chunks(), Is.Empty);
        }

        [Test]
        public void RegionsCombineAcrossAStroke()
        {
            var first = DirtyRegion.FromSamples(10, 10, 20, 20);
            var second = DirtyRegion.FromSamples(40, 5, 50, 15);
            var both = first.Union(second);

            Assert.That(both.MinX, Is.EqualTo(10));
            Assert.That(both.MinZ, Is.EqualTo(5));
            Assert.That(both.MaxX, Is.EqualTo(50));
            Assert.That(both.MaxZ, Is.EqualTo(20));
            Assert.That(first.Union(DirtyRegion.Empty), Is.EqualTo(first));
            Assert.That(DirtyRegion.Empty.Union(second), Is.EqualTo(second));
        }

        [Test]
        public void EveryChunkTheIslandHasCanBeReached()
        {
            // If the arithmetic quietly clamped, some corner of the island could never be
            // rebuilt — and would silently stop responding to the brush.
            var reached = new HashSet<(int, int)>();
            for (var cz = 0; cz < IslandConstants.ChunksPerSide; cz++)
            for (var cx = 0; cx < IslandConstants.ChunksPerSide; cx++)
            {
                var sampleX = cx * IslandConstants.ChunkQuadsPerSide + 16;
                var sampleZ = cz * IslandConstants.ChunkQuadsPerSide + 16;
                var field = new Heightfield();
                var region = TerrainSculptor.Apply(field,
                    new TerrainBrush(BrushKind.Raise,
                        Heightfield.WorldXOf(sampleX), Heightfield.WorldZOf(sampleZ), 1f, 2f), 1f / 60f);
                foreach (var chunk in region.Chunks()) reached.Add(chunk);
            }

            Assert.That(reached.Count, Is.EqualTo(IslandConstants.ChunkCount),
                "every one of the island's 64 chunks must be reachable by a brush");
        }
    }
}
