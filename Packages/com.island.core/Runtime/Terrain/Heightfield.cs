using System;

namespace Island.Core.Terrain
{
    /// <summary>
    /// The island's ground: one height and one set of surface weights per sample.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is exactly one of these for the whole island, shared by all 64 chunks. That is the
    /// single most important decision about the terrain, and it is worth being explicit about
    /// why: chunks that each kept their own copy of their edge would drift apart by a fraction of
    /// a millimetre and leave a crack you can see through. Sharing the samples makes that
    /// arithmetically impossible rather than merely unlikely.
    /// </para>
    /// <para>
    /// Weights are bytes summing to 255, not floats summing to 1. They go into a vertex colour,
    /// so that is the precision the GPU will actually see; keeping floats here would only hide
    /// the rounding until later.
    /// </para>
    /// </remarks>
    public sealed class Heightfield
    {
        private const int Resolution = IslandConstants.HeightfieldResolution;
        private const int Channels = IslandConstants.SurfaceChannelCount;

        private readonly float[] _heights;
        private readonly byte[] _weights;
        private bool[] _locked;

        /// <summary>A flat island of dry sand, which is where every island starts.</summary>
        public Heightfield()
        {
            _heights = new float[Resolution * Resolution];
            _weights = new byte[Resolution * Resolution * Channels];
            for (var i = 0; i < Resolution * Resolution; i++)
            {
                _weights[i * Channels + IslandConstants.SurfaceChannelSand] = 255;
            }
        }

        public int SampleResolution => Resolution;

        // ---- world and grid -----------------------------------------------------------------------

        /// <summary>The world position of a sample. Sample 0 sits on the island's edge, not inside it.</summary>
        public static float WorldXOf(int gridX) =>
            -IslandConstants.IslandHalfExtentMetres + gridX * IslandConstants.VertexSpacingMetres;

        public static float WorldZOf(int gridZ) => WorldXOf(gridZ);

        /// <summary>The nearest sample to a world coordinate, clamped to the island.</summary>
        public static int GridOf(float world)
        {
            var exact = (world + IslandConstants.IslandHalfExtentMetres) / IslandConstants.VertexSpacingMetres;
            var index = (int)Math.Round(exact, MidpointRounding.AwayFromZero);
            return index < 0 ? 0 : index > Resolution - 1 ? Resolution - 1 : index;
        }

        public static bool InRange(int gridX, int gridZ) =>
            gridX >= 0 && gridX < Resolution && gridZ >= 0 && gridZ < Resolution;

        private static int Index(int gridX, int gridZ) => gridZ * Resolution + gridX;

        // ---- heights ------------------------------------------------------------------------------

        public float HeightAt(int gridX, int gridZ)
        {
            if (!InRange(gridX, gridZ))
                throw new ArgumentOutOfRangeException(nameof(gridX), $"({gridX}, {gridZ}) is off the island");
            return _heights[Index(gridX, gridZ)];
        }

        public void SetHeight(int gridX, int gridZ, float metres)
        {
            if (!InRange(gridX, gridZ))
                throw new ArgumentOutOfRangeException(nameof(gridX), $"({gridX}, {gridZ}) is off the island");
            _heights[Index(gridX, gridZ)] = IslandConstants.ClampHeight(metres);
        }

        /// <summary>The height under a world position, interpolated between the four samples around it.</summary>
        public float HeightAtWorld(float worldX, float worldZ)
        {
            var exactX = (worldX + IslandConstants.IslandHalfExtentMetres) / IslandConstants.VertexSpacingMetres;
            var exactZ = (worldZ + IslandConstants.IslandHalfExtentMetres) / IslandConstants.VertexSpacingMetres;
            var x0 = Clamp((int)Math.Floor(exactX));
            var z0 = Clamp((int)Math.Floor(exactZ));
            var x1 = Clamp(x0 + 1);
            var z1 = Clamp(z0 + 1);
            var fx = exactX - x0;
            var fz = exactZ - z0;
            if (fx < 0f) fx = 0f; else if (fx > 1f) fx = 1f;
            if (fz < 0f) fz = 0f; else if (fz > 1f) fz = 1f;

            var near = Lerp(HeightAt(x0, z0), HeightAt(x1, z0), (float)fx);
            var far = Lerp(HeightAt(x0, z1), HeightAt(x1, z1), (float)fx);
            return Lerp(near, far, (float)fz);
        }

        /// <summary>
        /// How steep the ground is at a sample, in degrees, from the slope across its neighbours.
        /// </summary>
        /// <remarks>
        /// Measured across both neighbours where there are two, and one-sided at the island's
        /// edge. A one-sided difference at the edge reads as half the true slope, so the edge
        /// samples are doubled to match.
        /// </remarks>
        public float SlopeDegreesAt(int gridX, int gridZ)
        {
            var spacing = IslandConstants.VertexSpacingMetres;
            var west = HeightAt(Clamp(gridX - 1), gridZ);
            var east = HeightAt(Clamp(gridX + 1), gridZ);
            var south = HeightAt(gridX, Clamp(gridZ - 1));
            var north = HeightAt(gridX, Clamp(gridZ + 1));

            var spanX = (Clamp(gridX + 1) - Clamp(gridX - 1)) * spacing;
            var spanZ = (Clamp(gridZ + 1) - Clamp(gridZ - 1)) * spacing;
            var slopeX = spanX > 0 ? (east - west) / spanX : 0f;
            var slopeZ = spanZ > 0 ? (north - south) / spanZ : 0f;

            var gradient = Math.Sqrt(slopeX * slopeX + slopeZ * slopeZ);
            return (float)(Math.Atan(gradient) * 180.0 / Math.PI);
        }

        /// <summary>
        /// Whether rock should show through here because the ground is too steep to hold anything.
        /// </summary>
        /// <remarks>
        /// Not painted and not stored: it follows from the shape of the ground, so sculpting a
        /// cliff makes it rocky and flattening it makes it grassy again, with nothing to keep in
        /// step. One threshold, from <see cref="IslandConstants.AutoRockSlopeDegrees"/>.
        /// </remarks>
        public bool IsRockBySlope(int gridX, int gridZ) =>
            SlopeDegreesAt(gridX, gridZ) >= IslandConstants.AutoRockSlopeDegrees;

        // ---- surface weights ----------------------------------------------------------------------

        public byte WeightAt(int gridX, int gridZ, int channel)
        {
            if (!InRange(gridX, gridZ))
                throw new ArgumentOutOfRangeException(nameof(gridX), $"({gridX}, {gridZ}) is off the island");
            if (channel < 0 || channel >= Channels)
                throw new ArgumentOutOfRangeException(nameof(channel), $"channel {channel} does not exist");
            return _weights[Index(gridX, gridZ) * Channels + channel];
        }

        /// <summary>
        /// Lays <paramref name="amount"/> of one surface down, taking it from the others in
        /// proportion so the four always sum to 255.
        /// </summary>
        public void BlendWeight(int gridX, int gridZ, int channel, float amount)
        {
            if (amount <= 0f) return;
            if (amount > 1f) amount = 1f;
            var at = Index(gridX, gridZ) * Channels;

            var target = _weights[at + channel];
            var gain = (int)Math.Round((255 - target) * amount);
            if (gain <= 0) return;

            var others = 255 - target;
            if (others <= 0) return;

            var taken = 0;
            for (var c = 0; c < Channels && taken < gain; c++)
            {
                if (c == channel) continue;
                var share = (int)Math.Round(gain * (double)_weights[at + c] / others);
                if (share > _weights[at + c]) share = _weights[at + c];
                if (share > gain - taken) share = gain - taken;
                _weights[at + c] -= (byte)share;
                taken += share;
            }

            // Rounding can leave a unit or two; take it from whichever channel still has the most,
            // so the four keep summing to 255 exactly rather than drifting over a long stroke.
            while (taken < gain)
            {
                var fattest = -1;
                for (var c = 0; c < Channels; c++)
                {
                    if (c == channel || _weights[at + c] == 0) continue;
                    if (fattest < 0 || _weights[at + c] > _weights[at + fattest]) fattest = c;
                }
                if (fattest < 0) break;
                _weights[at + fattest]--;
                taken++;
            }

            _weights[at + channel] = (byte)(target + taken);
        }

        /// <summary>
        /// Writes all four weights at a sample directly.
        /// </summary>
        /// <remarks>
        /// For loading a save, where the weights are already known exactly and must come back
        /// exactly. Blending them in one at a time cannot do that: the first blend finds the
        /// channel already at its starting value and has nothing to take from, so a restored
        /// island would come back as the flat sand it started as.
        /// <para>
        /// Sculpting must not use this — <see cref="BlendWeight"/> is what keeps a brush stroke
        /// adding up.
        /// </para>
        /// </remarks>
        public void SetWeights(int gridX, int gridZ, byte sand, byte grass, byte rock, byte path)
        {
            if (!InRange(gridX, gridZ))
                throw new ArgumentOutOfRangeException(nameof(gridX), $"({gridX}, {gridZ}) is off the island");
            var total = sand + grass + rock + path;
            if (total != 255)
            {
                throw new ArgumentException(
                    $"Surface weights must sum to 255; {sand}+{grass}+{rock}+{path} is {total}.", nameof(sand));
            }
            var at = Index(gridX, gridZ) * Channels;
            _weights[at + IslandConstants.SurfaceChannelSand] = sand;
            _weights[at + IslandConstants.SurfaceChannelGrass] = grass;
            _weights[at + IslandConstants.SurfaceChannelRock] = rock;
            _weights[at + IslandConstants.SurfaceChannelPath] = path;
        }

        /// <summary>The four weights at a sample. Always sums to 255.</summary>
        public int WeightSumAt(int gridX, int gridZ)
        {
            var at = Index(gridX, gridZ) * Channels;
            var total = 0;
            for (var c = 0; c < Channels; c++) total += _weights[at + c];
            return total;
        }

        // ---- reserved ground ----------------------------------------------------------------------

        /// <summary>
        /// Marks samples that brushes must leave alone: the station's footprint and the track's
        /// corridor off the island (ADR-24). Loading and generating still write them; only
        /// <see cref="TerrainSculptor"/> refuses to.
        /// </summary>
        /// <remarks>
        /// Held here rather than passed to each stroke so that no caller can forget it: every
        /// brush goes through the same field. Undo and redo write back only what a stroke
        /// changed, and a stroke never changes a locked sample, so history cannot undo a lock
        /// that was in place when the stroke was made.
        /// </remarks>
        public void Lock(int minX, int minZ, int maxX, int maxZ)
        {
            if (_locked == null) _locked = new bool[Resolution * Resolution];
            for (var z = Math.Max(minZ, 0); z <= Math.Min(maxZ, Resolution - 1); z++)
            for (var x = Math.Max(minX, 0); x <= Math.Min(maxX, Resolution - 1); x++)
                _locked[Index(x, z)] = true;
        }

        /// <summary>Lifts every lock, for a field about to hold a different island.</summary>
        public void ClearLocks() => _locked = null;

        /// <summary>Whether brushes must leave this sample alone.</summary>
        public bool IsLocked(int gridX, int gridZ) =>
            _locked != null && InRange(gridX, gridZ) && _locked[Index(gridX, gridZ)];

        // ---- copying ------------------------------------------------------------------------------

        /// <summary>A snapshot of the heights, for undo and for brushes that read while writing.</summary>
        public float[] CopyHeights()
        {
            var copy = new float[_heights.Length];
            Array.Copy(_heights, copy, _heights.Length);
            return copy;
        }

        public void RestoreHeights(float[] heights)
        {
            if (heights == null || heights.Length != _heights.Length)
                throw new ArgumentException("That snapshot is not the right size for this island.", nameof(heights));
            Array.Copy(heights, _heights, _heights.Length);
        }

        internal float HeightByIndex(int index) => _heights[index];
        internal static int IndexOf(int gridX, int gridZ) => Index(gridX, gridZ);

        private static int Clamp(int grid) => grid < 0 ? 0 : grid > Resolution - 1 ? Resolution - 1 : grid;

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;
    }
}
