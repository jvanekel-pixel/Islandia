using Island.Core.Save;
using Island.Core.Terrain;
using Island.Core.World;
using NUnit.Framework;

namespace Island.Core.Tests.World
{
    /// <summary>
    /// The station and its track are the player's way home. Nothing they do should bury them.
    /// </summary>
    [TestFixture]
    public class StationReservationTests
    {
        private const int Last = IslandConstants.HeightfieldResolution - 1;

        private static (float[] Heights, byte[] Weights) Reserved(Heightfield field, StationSite site)
        {
            var heights = new float[IslandConstants.HeightfieldSampleCount];
            var weights = new byte[IslandConstants.HeightfieldSampleCount * IslandConstants.SurfaceChannelCount];
            for (var z = 0; z <= Last; z++)
            for (var x = 0; x <= Last; x++)
            {
                if (!site.IsReserved(x, z)) continue;
                var i = z * (Last + 1) + x;
                heights[i] = field.HeightAt(x, z);
                for (var c = 0; c < IslandConstants.SurfaceChannelCount; c++)
                    weights[i * IslandConstants.SurfaceChannelCount + c] = field.WeightAt(x, z, c);
            }
            return (heights, weights);
        }

        private static float CentreX(GridRect r) => Heightfield.WorldXOf((r.MinX + r.MaxX) / 2);
        private static float CentreZ(GridRect r) => Heightfield.WorldZOf((r.MinZ + r.MaxZ) / 2);

        /// <summary>Every brush, at full size and strength, dragged over the station and down its track.</summary>
        private static void SculptAcross(Heightfield field, StationSite site)
        {
            var centres = new[]
            {
                (CentreX(site.Footprint), CentreZ(site.Footprint)),
                (CentreX(site.Corridor), CentreZ(site.Corridor)),
                (Heightfield.WorldXOf(site.Footprint.MinX), Heightfield.WorldZOf(site.Footprint.MinZ)),
                (Heightfield.WorldXOf(site.Footprint.MaxX), Heightfield.WorldZOf(site.Footprint.MaxZ)),
            };
            foreach (BrushKind kind in System.Enum.GetValues(typeof(BrushKind)))
            foreach (var (x, z) in centres)
            for (var step = 0; step < 3; step++)
            {
                TerrainSculptor.Apply(field, new TerrainBrush(kind, x, z, TerrainBrush.MaxRadiusMetres, 6f,
                    surfaceChannel: IslandConstants.SurfaceChannelRock, flattenTargetMetres: 4f), 0.5f);
            }
        }

        [Test]
        public void NoBrushChangesTheStationOrItsTrack()
        {
            var island = IslandGenesis.Generate(7);
            var before = Reserved(island.Terrain, island.Station);
            var fingerprint = IslandGenesisTests.Fingerprint(island.Terrain);

            SculptAcross(island.Terrain, island.Station);

            var after = Reserved(island.Terrain, island.Station);
            Assert.That(after.Heights, Is.EqualTo(before.Heights), "a brush moved reserved ground");
            Assert.That(after.Weights, Is.EqualTo(before.Weights), "a brush painted reserved ground");
            Assert.That(IslandGenesisTests.Fingerprint(island.Terrain), Is.Not.EqualTo(fingerprint),
                "the brushes did change the ground around the station, so they really ran across it");
        }

        [Test]
        public void NothingCanBePlacedOnTheStationOrItsTrack()
        {
            var site = IslandGenesis.Generate(7).Station;
            int refused = 0, permitted = 0;

            // A one-metre object swept over the whole island in half-metre steps.
            for (var minZ = -32f; minZ < 32f; minZ += 0.5f)
            for (var minX = -32f; minX < 32f; minX += 0.5f)
            {
                var overlapsReservedGround = false;
                for (var z = Heightfield.GridOf(minZ); z <= Heightfield.GridOf(minZ + 1f); z++)
                for (var x = Heightfield.GridOf(minX); x <= Heightfield.GridOf(minX + 1f); x++)
                    overlapsReservedGround |= site.IsReserved(x, z);

                var allowed = site.Permits(minX, minZ, minX + 1f, minZ + 1f);
                Assert.That(allowed, Is.EqualTo(!overlapsReservedGround), $"a 1 m object at ({minX}, {minZ})");
                if (allowed) permitted++; else refused++;
            }

            Assert.That(refused, Is.GreaterThan(0));
            Assert.That(permitted, Is.GreaterThan(refused), "the rest of the island stays open");
        }

        [Test]
        public void TheReservationIsSavedWithTheIslandAndHoldsAfterLoading()
        {
            var island = IslandGenesis.Generate(7);
            var container = new SaveContainer(1_762_000_000_000L);
            container.Put(new TerrainSaveSection(island.Terrain));
            container.Put(new StationSaveSection(island.Station));
            var file = SaveContainer.FromBytes(container.ToBytes());

            var terrain = new Heightfield();
            file.ReadInto(new TerrainSaveSection(terrain));
            var station = new StationSaveSection(null, lockInto: terrain);
            Assert.That(file.ReadInto(station), Is.True);
            var site = station.Site;
            Assert.That(site.Direction, Is.EqualTo(island.Station.Direction));
            Assert.That(site.Footprint, Is.EqualTo(island.Station.Footprint));
            Assert.That(site.Corridor, Is.EqualTo(island.Station.Corridor));

            // Reading the section locked the ground; nothing else was called.
            var before = Reserved(terrain, site);
            SculptAcross(terrain, site);
            Assert.That(Reserved(terrain, site).Heights, Is.EqualTo(before.Heights));
        }

        [Test]
        public void GroundWithNoStationIsGivenOneWithoutDisturbingTheRest()
        {
            // A flat island to its edges, as a save from before the station existed would be:
            // the station is surveyed on, the ground in front of it cut back to water, and nothing else moves.
            var field = new Heightfield();
            TerrainSculptor.Apply(field, new TerrainBrush(BrushKind.Raise, 0f, 0f, 6f, 4f), 1f);
            var original = field.CopyHeights();

            var site = IslandGenesis.EnsureStation(field, 99);

            var margin = 3 * (int)(1f / IslandConstants.VertexSpacingMetres);
            for (var z = 0; z <= Last; z++)
            for (var x = 0; x <= Last; x++)
            {
                var height = field.HeightAt(x, z);
                if (site.Footprint.Contains(x, z))
                    Assert.That(height, Is.EqualTo(IslandConstants.StationPlatformHeightMetres));
                else if (site.Front.Contains(x, z))
                    Assert.That(IslandConstants.IsSubmerged(height), $"({x}, {z}) is dry in front of the station");
                else if (x < site.Footprint.MinX - margin || x > site.Footprint.MaxX + margin ||
                         z < site.Footprint.MinZ - margin || z > site.Footprint.MaxZ + margin)
                    Assert.That(height, Is.EqualTo(original[z * (Last + 1) + x]), $"({x}, {z}) moved");
                Assert.That(field.IsLocked(x, z), Is.EqualTo(site.IsReserved(x, z)), $"({x}, {z})");
            }
            var again = new Heightfield();
            Assert.That(IslandGenesis.EnsureStation(again, 99).Footprint, Is.EqualTo(site.Footprint),
                "the same seed surveys the same site");
        }
    }
}
