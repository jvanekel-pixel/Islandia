using System;
using Island.Core.Terrain;

namespace Island.Core.World
{
    /// <summary>A new island, as its seed made it.</summary>
    public sealed class GeneratedIsland
    {
        internal GeneratedIsland(ulong seed, Heightfield terrain, StationSite station, string theme)
        {
            Seed = seed;
            Terrain = terrain;
            Station = station;
            Theme = theme;
        }

        public ulong Seed { get; }
        public Heightfield Terrain { get; }
        public StationSite Station { get; }
        public string Theme { get; }
    }

    /// <summary>
    /// Makes an island's starting ground and station site from its seed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here is integer arithmetic on heights in 1/1024 m, converted to metres once
    /// at the end by a power-of-two division, which is exact. Floating-point maths is
    /// deterministic on one machine but not across them: IL2CPP on the headset may fuse a
    /// multiply and an add that Mono in the editor rounds twice, and <c>Math.Exp</c> differs
    /// between C libraries. A seed must give the same island everywhere, so none of that is used.
    /// </para>
    /// <para>
    /// The shape: a landmass near the middle, three to five lobes out towards the coast that make
    /// each coastline its own, all kept 2 m clear of the 64 m extent so open water rings every island; then the
    /// station site, found where the track's line first meets land; then up to two hills kept
    /// clear of it; then the station's platform levelled and its corridor kept as water.
    /// </para>
    /// </remarks>
    public static class IslandGenesis
    {
        /// <summary>Generated heights are whole multiples of 1/1024 m.</summary>
        public const int TicksPerMetre = 1024;

        /// <summary>
        /// Provisional theme ids, until <c>C-02c</c> admits pack items per theme. An island keeps
        /// the id it was given in the world index, so this list can grow without moving any.
        /// </summary>
        public static readonly string[] Themes = { "meadow", "woodland", "village", "harbour" };

        private const int N = IslandConstants.HeightfieldResolution;
        private const int Last = N - 1;
        private const int Centre = Last / 2;
        private const int PerMetre = (int)(1f / IslandConstants.VertexSpacingMetres);
        private const int FootprintLength = (int)(IslandConstants.StationFootprintLengthMetres * PerMetre);
        private const int FootprintHalf = (int)(IslandConstants.StationFootprintWidthMetres * PerMetre) / 2;
        private const int CorridorHalf = (int)(IslandConstants.TrackCorridorWidthMetres * PerMetre) / 2;
        private const int MinCorridor = (int)(IslandConstants.TrackCorridorMinLengthMetres * PerMetre);
        private const int BlendMargin = 2 * PerMetre;
        private const int LandReach = (int)((IslandConstants.IslandHalfExtentMetres - 2f) * PerMetre);
        private const int SeabedTicks = -5 * TicksPerMetre / 2;
        private const int PlatformTicks = (int)(IslandConstants.StationPlatformHeightMetres * TicksPerMetre);
        private static readonly int CorridorFloorTicks =
            (int)Math.Floor(IslandConstants.TrackCorridorFloorMetres * TicksPerMetre);
        private static readonly int GrassLineTicks = (int)Math.Floor(IslandConstants.GrassLineMetres * TicksPerMetre);
        private static readonly int GrassSpanTicks =
            (int)Math.Floor((IslandConstants.FullGrassMetres - IslandConstants.GrassLineMetres) * TicksPerMetre);
        private const long One = 1L << 16;

        public static string ThemeFor(ulong seed) =>
            Themes[SeedStream.For(seed, SeedStream.Theme).Range(0, Themes.Length - 1)];

        public static GeneratedIsland Generate(ulong seed)
        {
            var ticks = new int[N * N];
            for (var i = 0; i < ticks.Length; i++) ticks[i] = SeabedTicks;
            var rng = SeedStream.For(seed, SeedStream.Terrain);

            Landmass(ticks, Centre + rng.Range(-PerMetre, PerMetre), Centre + rng.Range(-PerMetre, PerMetre),
                     rng.Range(22 * PerMetre, 25 * PerMetre), rng.Range(600, 1200));

            var lobes = rng.Range(3, 5);
            for (var lobe = 0; lobe < lobes; lobe++)
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var radius = rng.Range(10 * PerMetre, 16 * PerMetre);
                var dx = rng.Range(-20 * PerMetre, 20 * PerMetre);
                var dz = rng.Range(-20 * PerMetre, 20 * PerMetre);
                var plateau = rng.Range(400, 1000);
                // Out towards the coast, where a lobe changes its shape, and never past the reach.
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) < 12 * PerMetre) continue;
                if (Math.Abs(dx) + radius > LandReach || Math.Abs(dz) + radius > LandReach) continue;
                var x = Centre + dx;
                var z = Centre + dz;
                Landmass(ticks, x, z, radius, plateau);
                break;
            }

            var site = Survey(ticks, SeedStream.For(seed, SeedStream.Station));

            var hills = rng.Range(0, 2);
            for (var hill = 0; hill < hills; hill++)
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var radius = rng.Range(4 * PerMetre, 7 * PerMetre);
                var x = Centre + rng.Range(-10 * PerMetre, 10 * PerMetre);
                var z = Centre + rng.Range(-10 * PerMetre, 10 * PerMetre);
                var peak = rng.Range(3 * TicksPerMetre / 2, 4 * TicksPerMetre);
                var reach = radius + BlendMargin;
                var clear = new GridRect(x - reach, z - reach, x + reach, z + reach);
                if (clear.Intersects(site.Footprint) || clear.Intersects(site.Front)) continue;
                Hill(ticks, x, z, radius, peak);
                break;
            }

            Stamp(ticks, site);

            var field = new Heightfield();
            for (var z = 0; z < N; z++)
            for (var x = 0; x < N; x++)
            {
                field.SetHeight(x, z, ticks[z * N + x] / (float)TicksPerMetre);
                PaintByHeight(field, x, z, ticks[z * N + x]);
            }
            PaintPlatform(field, site);
            site.LockInto(field);
            return new GeneratedIsland(seed, field, site, ThemeFor(seed));
        }

        /// <summary>
        /// Gives ground that has no station one, from a seed: the same survey and levelling a new
        /// island gets, changing only the samples it must. For a save from before the world of
        /// islands, and for an island file that has lost its station section.
        /// </summary>
        public static StationSite EnsureStation(Heightfield field, ulong seed)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            var ticks = new int[N * N];
            for (var z = 0; z < N; z++)
            for (var x = 0; x < N; x++)
                ticks[z * N + x] = (int)Math.Round(field.HeightAt(x, z) * (double)TicksPerMetre, MidpointRounding.AwayFromZero);

            var before = (int[])ticks.Clone();
            var site = Survey(ticks, SeedStream.For(seed, SeedStream.Station));
            Stamp(ticks, site);

            for (var z = 0; z < N; z++)
            for (var x = 0; x < N; x++)
            {
                var i = z * N + x;
                if (ticks[i] == before[i]) continue;
                field.SetHeight(x, z, ticks[i] / (float)TicksPerMetre);
                // Ground the station moved takes the surface of its new height: no grass under the track.
                PaintByHeight(field, x, z, ticks[i]);
            }
            PaintPlatform(field, site);
            site.LockInto(field);
            return site;
        }

        // ---- the station ---------------------------------------------------------------------------

        /// <summary>
        /// Picks a side and a line across it, then walks in from the island's edge until the
        /// station's width first meets land: that is the shore, and the station's seaward edge, so
        /// nothing in front of it is land.
        /// </summary>
        private static StationSite Survey(int[] ticks, SeedStream rng)
        {
            var direction = (TrackDirection)rng.Range(0, 3);
            var across = Centre + rng.Range(-6 * PerMetre, 6 * PerMetre);

            var shore = -1;
            for (var inland = 0; inland <= Centre && shore < 0; inland++)
            for (var b = across - FootprintHalf; b <= across + FootprintHalf; b++)
            {
                var (x, z) = StationSite.ToGrid(direction, inland, b);
                if (IslandConstants.IsSubmerged(ticks[z * N + x] / (float)TicksPerMetre)) continue;
                shore = inland;
                break;
            }
            // No land on this line at all: the station makes its own ground in the middle.
            if (shore < 0) shore = Centre - FootprintLength / 2;
            // Land right up to the edge: the corridor is cut back to water in front of the station.
            if (shore < MinCorridor) shore = MinCorridor;

            return new StationSite(
                direction,
                StationSite.RectInFrame(direction, shore, shore + FootprintLength, across - FootprintHalf, across + FootprintHalf),
                StationSite.RectInFrame(direction, 0, shore, across - CorridorHalf, across + CorridorHalf));
        }

        /// <summary>
        /// Levels the footprint to a dry platform, keeps the water in front of it — the station's
        /// width, out to the edge, which holds the corridor — at or below the corridor's floor, and eases the
        /// ground beside and behind the station towards the platform so it sits in the land.
        /// </summary>
        private static void Stamp(int[] ticks, StationSite site)
        {
            var footprint = site.Footprint;
            var shore = Math.Min(StationSite.InlandOf(site.Direction, footprint.MinX, footprint.MinZ),
                                 StationSite.InlandOf(site.Direction, footprint.MaxX, footprint.MaxZ));
            for (var z = 0; z < N; z++)
            for (var x = 0; x < N; x++)
            {
                var i = z * N + x;
                if (footprint.Contains(x, z))
                {
                    ticks[i] = PlatformTicks;
                }
                else if (site.Front.Contains(x, z))
                {
                    ticks[i] = Math.Min(ticks[i], CorridorFloorTicks);
                }
                else
                {
                    var gap = Math.Max(Math.Max(footprint.MinX - x, x - footprint.MaxX),
                                       Math.Max(footprint.MinZ - z, z - footprint.MaxZ));
                    if (gap > BlendMargin) continue;
                    // Seaward of the station stays as it is: that is the view down the line.
                    if (StationSite.InlandOf(site.Direction, x, z) < shore) continue;
                    ticks[i] += (PlatformTicks - ticks[i]) * (BlendMargin + 1 - gap) / (BlendMargin + 1);
                }
            }
        }

        /// <summary>Sand on the beach, grass above the grass line, eased between.</summary>
        private static void PaintByHeight(Heightfield field, int x, int z, int heightTicks)
        {
            var grass = (byte)Math.Max(0, Math.Min(255, (heightTicks - GrassLineTicks) * 255 / GrassSpanTicks));
            field.SetWeights(x, z, (byte)(255 - grass), grass, 0, 0);
        }

        private static void PaintPlatform(Heightfield field, StationSite site)
        {
            var footprint = site.Footprint;
            for (var z = footprint.MinZ; z <= footprint.MaxZ; z++)
            for (var x = footprint.MinX; x <= footprint.MaxX; x++)
                field.SetWeights(x, z, 0, 0, 0, 255);
        }

        // ---- shapes, in fixed point ------------------------------------------------------------------

        /// <summary>A rounded plateau: flat out to 0.7 of its radius, then a beach down to the seabed.</summary>
        private static void Landmass(int[] ticks, int cx, int cz, int radius, int plateau)
        {
            long r2 = (long)radius * radius;
            for (var z = Math.Max(0, cz - radius); z <= Math.Min(Last, cz + radius); z++)
            for (var x = Math.Max(0, cx - radius); x <= Math.Min(Last, cx + radius); x++)
            {
                long d2 = (long)(x - cx) * (x - cx) + (long)(z - cz) * (z - cz);
                if (d2 >= r2) continue;
                var height = (int)(SeabedTicks + (plateau - SeabedTicks) * Smooth((r2 - d2) * 2 * One / r2) / One);
                if (height > ticks[z * N + x]) ticks[z * N + x] = height;
            }
        }

        private static void Hill(int[] ticks, int cx, int cz, int radius, int peak)
        {
            long r2 = (long)radius * radius;
            for (var z = Math.Max(0, cz - radius); z <= Math.Min(Last, cz + radius); z++)
            for (var x = Math.Max(0, cx - radius); x <= Math.Min(Last, cx + radius); x++)
            {
                long d2 = (long)(x - cx) * (x - cx) + (long)(z - cz) * (z - cz);
                if (d2 >= r2) continue;
                ticks[z * N + x] += (int)(peak * Smooth((r2 - d2) * One / r2) / One);
            }
        }

        /// <summary>Smoothstep in 16.16 fixed point: 0 and 1 at the ends, flat at both.</summary>
        private static long Smooth(long t)
        {
            if (t <= 0) return 0;
            if (t >= One) return One;
            return t * t / One * (3 * One - 2 * t) / One;
        }
    }
}
