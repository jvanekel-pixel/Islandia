using System;
using Island.Core.Terrain;

namespace Island.Core.Rides
{
    /// <summary>
    /// Where a coaster's grid is on the island. The mapping is fixed and exact: a coaster's grid
    /// <em>is</em> the island's world space, measured in sub-units.
    /// </summary>
    /// <remarks>
    /// The origin is the island's centre at height 0 (world origin), and the axes are the
    /// island's: +X east, +Y up, +Z north. A coaster is put somewhere by its
    /// <see cref="Coaster.Start"/>, which may be any sub-unit, so nothing about a coaster's place
    /// is stored twice. A sub-unit is 1/32 m, a power of two, so every track position converts to
    /// metres exactly, and every heightfield sample (0.25 m apart) is a whole eight sub-units.
    /// </remarks>
    public static class CoasterSpace
    {
        /// <summary><see cref="CoasterShapes.SubUnitsPerUnit"/> over <see cref="CoasterPieces.MetresPerUnit"/>.</summary>
        public const int SubUnitsPerMetre = 32;

        /// <summary>The heightfield's sample spacing, 0.25 m, in sub-units.</summary>
        public const int SubUnitsPerSample = 8;

        /// <summary>Half the island's 64 m extent, in sub-units: the edge is at ±1024 in X and Z.</summary>
        public const int IslandHalfExtent = 1024;

        /// <summary>Sub-units to metres, exactly.</summary>
        public static float MetresOf(int subUnits) => subUnits / (float)SubUnitsPerMetre;

        /// <summary>A track point's place in the world, in metres, exactly.</summary>
        public static (float X, float Y, float Z) WorldOf(TrackPoint point) =>
            (MetresOf(point.X), MetresOf(point.Y), MetresOf(point.Z));

        /// <summary>Whether a point lies over the island's ground, its edge included.</summary>
        public static bool IsOverIsland(int x, int z) =>
            x >= -IslandHalfExtent && x <= IslandHalfExtent && z >= -IslandHalfExtent && z <= IslandHalfExtent;
    }

    /// <summary>The ground under a coaster, as validation sees it.</summary>
    public interface ICoasterGround
    {
        /// <summary>
        /// The ground's height at (<paramref name="x"/>, <paramref name="z"/>) in the coaster's
        /// grid, in sub-units, rounded up; false where there is no ground (off the island).
        /// </summary>
        bool TryGetGround(int x, int z, out int height);
    }

    /// <summary>Level ground at one height, everywhere. For tests and the tabletop preview.</summary>
    public sealed class FlatGround : ICoasterGround
    {
        public FlatGround(int height) => Height = height;

        public int Height { get; }

        public bool TryGetGround(int x, int z, out int height)
        {
            height = Height;
            return true;
        }
    }

    /// <summary>
    /// The island's own ground, read from its <see cref="Heightfield"/> in integers.
    /// </summary>
    /// <remarks>
    /// Interpolated between the four samples around a point, as <see cref="Heightfield.HeightAtWorld"/>
    /// does, but in fixed point: each sample's height is taken in 1/1024 sub-unit (a float
    /// multiplied by a power of two, so exact, then rounded up), and the weights are whole
    /// eighths of a sample. The answer is the same under CoreCLR, Mono and IL2CPP, which floating
    /// interpolation would not promise.
    /// </remarks>
    public sealed class HeightfieldGround : ICoasterGround
    {
        private const int Step = CoasterSpace.SubUnitsPerSample;
        private const long Fraction = 1024;
        private readonly Heightfield _field;

        public HeightfieldGround(Heightfield field) =>
            _field = field ?? throw new ArgumentNullException(nameof(field));

        public bool TryGetGround(int x, int z, out int height)
        {
            height = 0;
            if (!CoasterSpace.IsOverIsland(x, z)) return false;
            var fromWest = x + CoasterSpace.IslandHalfExtent;
            var fromSouth = z + CoasterSpace.IslandHalfExtent;
            int gx = fromWest / Step, fx = fromWest % Step;
            int gz = fromSouth / Step, fz = fromSouth % Step;
            var gx1 = fx == 0 ? gx : gx + 1;
            var gz1 = fz == 0 ? gz : gz + 1;

            var sum = Fixed(gx, gz) * (Step - fx) * (Step - fz)
                    + Fixed(gx1, gz) * fx * (Step - fz)
                    + Fixed(gx, gz1) * (Step - fx) * fz
                    + Fixed(gx1, gz1) * fx * fz;
            height = (int)CeilingDivide(sum, Fraction * Step * Step);
            return true;
        }

        private long Fixed(int gx, int gz) =>
            (long)Math.Ceiling(_field.HeightAt(gx, gz) * (float)(CoasterSpace.SubUnitsPerMetre * Fraction));

        private static long CeilingDivide(long value, long by) =>
            value >= 0 ? (value + by - 1) / by : -(-value / by);
    }
}
