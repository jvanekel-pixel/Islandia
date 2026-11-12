using System;
using System.Collections.Generic;
using Island.Core.Terrain;
using Island.Core.World;
using NUnit.Framework;

namespace Island.Core.Tests.World
{
    /// <summary>
    /// Does a seed give one island, the same everywhere, with its station on the shore?
    /// </summary>
    [TestFixture]
    public class IslandGenesisTests
    {
        private const int Last = IslandConstants.HeightfieldResolution - 1;

        /// <summary>FNV-1a over every height's exact bits and every weight.</summary>
        internal static ulong Fingerprint(Heightfield field)
        {
            var hash = 0xCBF29CE484222325UL;
            void Add(long value)
            {
                for (var b = 0; b < 8; b++) hash = unchecked((hash ^ (byte)(value >> (8 * b))) * 0x100000001B3UL);
            }
            for (var z = 0; z <= Last; z++)
            for (var x = 0; x <= Last; x++)
            {
                Add(BitConverter.DoubleToInt64Bits(field.HeightAt(x, z)));
                for (var c = 0; c < IslandConstants.SurfaceChannelCount; c++) Add(field.WeightAt(x, z, c));
            }
            return hash;
        }

        private static IEnumerable<ulong> ManySeeds()
        {
            var stream = new SeedStream(2026);
            for (var i = 0; i < 150; i++) yield return stream.NextULong();
        }

        [Test]
        public void TheSeedStreamIsSplitMix64AsPublished()
        {
            // The reference outputs for seed 0. Anything else here would mean a seed no longer
            // means what it meant when the island was made.
            var stream = new SeedStream(0);
            Assert.That(stream.NextULong(), Is.EqualTo(0xE220A8397B1DCDAFUL));
            Assert.That(stream.NextULong(), Is.EqualTo(0x6E789E6AA1B965F4UL));
            Assert.That(stream.NextULong(), Is.EqualTo(0x06C45D188009454FUL));
        }

        [Test]
        public void TheSameSeedGivesTheSameIslandSampleForSample()
        {
            var first = IslandGenesis.Generate(42);
            var second = IslandGenesis.Generate(42);
            Assert.That(Fingerprint(second.Terrain), Is.EqualTo(Fingerprint(first.Terrain)));
            Assert.That(second.Station.Footprint, Is.EqualTo(first.Station.Footprint));
            Assert.That(second.Station.Corridor, Is.EqualTo(first.Station.Corridor));
            Assert.That(second.Theme, Is.EqualTo(first.Theme));
        }

        [Test]
        public void KnownSeedsArePinnedOnEveryRuntime()
        {
            // These run under CoreCLR in `make core-test` and under Mono in Unity's EditMode job.
            // The same numbers in both is the evidence that a seed is platform-independent.
            Assert.That(Fingerprint(IslandGenesis.Generate(1).Terrain), Is.EqualTo(15280648246636265048UL), "seed 1");
            Assert.That(Fingerprint(IslandGenesis.Generate(0xC0FFEE).Terrain), Is.EqualTo(10035519950499899449UL), "seed 0xC0FFEE");
            var station = IslandGenesis.Generate(1).Station;
            Assert.That(station.Direction, Is.EqualTo(TrackDirection.North));
            Assert.That(station.Footprint.ToString(), Is.EqualTo("[110..142] x [181..229]"));
        }

        [Test]
        public void DifferentSeedsGiveDifferentIslands()
        {
            var fingerprints = new HashSet<ulong>();
            var directions = new HashSet<TrackDirection>();
            var themes = new HashSet<string>();
            for (ulong seed = 0; seed < 40; seed++)
            {
                var island = IslandGenesis.Generate(seed);
                fingerprints.Add(Fingerprint(island.Terrain));
                directions.Add(island.Station.Direction);
                themes.Add(island.Theme);
                Assert.That(IslandGenesis.Themes, Does.Contain(island.Theme));
            }
            Assert.That(fingerprints.Count, Is.EqualTo(40));
            Assert.That(directions.Count, Is.EqualTo(4), "stations face every way");
            Assert.That(themes.Count, Is.GreaterThan(1));
        }

        [Test]
        public void EveryIslandLiesInsideTheExtentRingedByOpenWater()
        {
            foreach (var seed in ManySeeds()) IsInsideTheExtentRingedByOpenWater(seed);
        }

        [Test]
        public void TheStationStandsOnTheShoreFacingOpenWater()
        {
            foreach (var seed in ManySeeds()) StandsOnTheShoreFacingOpenWater(seed);
        }

        private static void IsInsideTheExtentRingedByOpenWater(ulong seed)
        {
            var field = IslandGenesis.Generate(seed).Terrain;
            var land = 0;
            for (var z = 0; z <= Last; z++)
            for (var x = 0; x <= Last; x++)
            {
                var height = field.HeightAt(x, z);
                if (!IslandConstants.IsSubmerged(height)) land++;
                if (x == 0 || z == 0 || x == Last || z == Last)
                    Assert.That(IslandConstants.IsSubmerged(height), $"({x}, {z}) is land on the edge of the extent");
            }
            // Something worth standing on: at least a fifth of the 64 m square.
            Assert.That(land, Is.GreaterThan(IslandConstants.HeightfieldSampleCount / 5), $"seed {seed}");
        }

        private static void StandsOnTheShoreFacingOpenWater(ulong seed)
        {
            var island = IslandGenesis.Generate(seed);
            var field = island.Terrain;
            var site = island.Station;
            var footprint = site.Footprint;
            var corridor = site.Corridor;
            var spacing = IslandConstants.VertexSpacingMetres;

            // The station is a dry, level platform of the stated size.
            var along = site.Direction == TrackDirection.East || site.Direction == TrackDirection.West;
            var lengthSamples = along ? footprint.MaxX - footprint.MinX : footprint.MaxZ - footprint.MinZ;
            var widthSamples = along ? footprint.MaxZ - footprint.MinZ : footprint.MaxX - footprint.MinX;
            Assert.That(lengthSamples * spacing, Is.EqualTo(IslandConstants.StationFootprintLengthMetres));
            Assert.That(widthSamples * spacing, Is.EqualTo(IslandConstants.StationFootprintWidthMetres));
            for (var z = footprint.MinZ; z <= footprint.MaxZ; z++)
            for (var x = footprint.MinX; x <= footprint.MaxX; x++)
                Assert.That(field.HeightAt(x, z), Is.EqualTo(IslandConstants.StationPlatformHeightMetres));

            // In front of it, across its whole width, everything is water out to the edge of the extent.
            var front = site.Front;
            Assert.That(front.Contains(corridor.MinX, corridor.MinZ) && front.Contains(corridor.MaxX, corridor.MaxZ));
            for (var z = front.MinZ; z <= front.MaxZ; z++)
            for (var x = front.MinX; x <= front.MaxX; x++)
                if (!footprint.Contains(x, z))
                    Assert.That(IslandConstants.IsSubmerged(field.HeightAt(x, z)), $"({x}, {z}) is dry under the track (seed {seed})");

            int seawardEdge, corridorFar, corridorNear, stationFront, behind;
            switch (site.Direction)
            {
                case TrackDirection.East: seawardEdge = corridor.MaxX; corridorFar = Last; corridorNear = corridor.MinX; stationFront = footprint.MaxX; behind = footprint.MinX - 1; break;
                case TrackDirection.West: seawardEdge = corridor.MinX; corridorFar = 0; corridorNear = corridor.MaxX; stationFront = footprint.MinX; behind = footprint.MaxX + 1; break;
                case TrackDirection.North: seawardEdge = corridor.MaxZ; corridorFar = Last; corridorNear = corridor.MinZ; stationFront = footprint.MaxZ; behind = footprint.MinZ - 1; break;
                default: seawardEdge = corridor.MinZ; corridorFar = 0; corridorNear = corridor.MaxZ; stationFront = footprint.MinZ; behind = footprint.MaxZ + 1; break;
            }
            Assert.That(seawardEdge, Is.EqualTo(corridorFar), "the corridor reaches the edge the station faces");
            Assert.That(corridorNear, Is.EqualTo(stationFront), "the corridor starts at the station's seaward edge");
            Assert.That(Math.Abs(corridorNear - corridorFar) * spacing,
                Is.GreaterThanOrEqualTo(IslandConstants.TrackCorridorMinLengthMetres));

            // And behind it is the island, not a pier standing in the sea.
            var middle = along ? (footprint.MinZ + footprint.MaxZ) / 2 : (footprint.MinX + footprint.MaxX) / 2;
            var landward = along ? field.HeightAt(behind, middle) : field.HeightAt(middle, behind);
            Assert.That(IslandConstants.IsSubmerged(landward), Is.False, "the ground behind the station is land");
        }

        [Test]
        public void TheThemeComesFromTheSeed()
        {
            Assert.That(IslandGenesis.Generate(9).Theme, Is.EqualTo(IslandGenesis.ThemeFor(9)));
            Assert.That(IslandGenesis.ThemeFor(9), Is.EqualTo(IslandGenesis.ThemeFor(9)));
        }
    }
}
