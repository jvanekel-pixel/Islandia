using System;
using System.Collections.Generic;

namespace Island.Core.Terrain
{
    /// <summary>
    /// Which part of the island a brush stroke actually changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rebuilding a chunk mesh is the expensive half of sculpting, so this has to be tight. Too
    /// wide and every stroke rebuilds ground that did not move; too narrow and a chunk keeps a
    /// stale edge, which is a crack in the terrain you can see daylight through.
    /// </para>
    /// <para>
    /// The chunks are held as a 64-bit mask rather than a bounding rectangle, because a brush is
    /// a <em>circle</em> and the corners of its bounding box are not inside it. A rectangle
    /// reported nine chunks for a stroke that changed five — four needless mesh rebuilds, on the
    /// stroke people hold down the longest. The island has exactly 64 chunks, so one
    /// <see cref="ulong"/> holds them with nothing to spare and nothing wasted.
    /// </para>
    /// <para>
    /// A sample sitting on a chunk border belongs to the chunks on <em>both</em> sides of it.
    /// That is not an edge case to tidy away: the island shares one heightfield precisely so two
    /// chunks agree exactly along their seam, and this is where that promise is kept.
    /// </para>
    /// </remarks>
    public readonly struct DirtyRegion : IEquatable<DirtyRegion>
    {
        private readonly ulong _chunks;

        private DirtyRegion(ulong chunks, int minX, int minZ, int maxX, int maxZ)
        {
            _chunks = chunks;
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        /// <summary>Nothing changed.</summary>
        public static readonly DirtyRegion Empty = new DirtyRegion(0UL, int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);

        public bool IsEmpty => _chunks == 0UL;

        /// <summary>Inclusive grid bounds of the samples that changed. Meaningless when empty.</summary>
        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }

        /// <summary>How many chunk meshes must be rebuilt.</summary>
        public int ChunkCount
        {
            get
            {
                // A plain loop rather than BitOperations.PopCount: this has to compile under
                // Unity's netstandard2.1 profile, and 64 iterations of nothing is not a cost.
                var count = 0;
                var bits = _chunks;
                while (bits != 0UL)
                {
                    bits &= bits - 1UL;
                    count++;
                }
                return count;
            }
        }

        /// <summary>The chunks to rebuild, as (x, z) pairs.</summary>
        public IEnumerable<(int X, int Z)> Chunks()
        {
            for (var chunk = 0; chunk < IslandConstants.ChunkCount; chunk++)
            {
                if ((_chunks & (1UL << chunk)) != 0UL)
                    yield return (chunk % IslandConstants.ChunksPerSide, chunk / IslandConstants.ChunksPerSide);
            }
        }

        public bool Contains(int chunkX, int chunkZ)
        {
            if (chunkX < 0 || chunkZ < 0 ||
                chunkX >= IslandConstants.ChunksPerSide || chunkZ >= IslandConstants.ChunksPerSide) return false;
            return (_chunks & (1UL << (chunkZ * IslandConstants.ChunksPerSide + chunkX))) != 0UL;
        }

        /// <summary>This region plus one changed sample, and the chunks that own it.</summary>
        public DirtyRegion WithSample(int gridX, int gridZ)
        {
            var (xFrom, xTo) = ChunkSpan(gridX, gridX);
            var (zFrom, zTo) = ChunkSpan(gridZ, gridZ);

            var chunks = _chunks;
            for (var cz = zFrom; cz <= zTo; cz++)
            for (var cx = xFrom; cx <= xTo; cx++)
                chunks |= 1UL << (cz * IslandConstants.ChunksPerSide + cx);

            return new DirtyRegion(chunks,
                Math.Min(IsEmpty ? int.MaxValue : MinX, gridX),
                Math.Min(IsEmpty ? int.MaxValue : MinZ, gridZ),
                Math.Max(IsEmpty ? int.MinValue : MaxX, gridX),
                Math.Max(IsEmpty ? int.MinValue : MaxZ, gridZ));
        }

        /// <summary>The union of two regions, so a stroke can accumulate across frames.</summary>
        public DirtyRegion Union(DirtyRegion other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;
            return new DirtyRegion(_chunks | other._chunks,
                Math.Min(MinX, other.MinX), Math.Min(MinZ, other.MinZ),
                Math.Max(MaxX, other.MaxX), Math.Max(MaxZ, other.MaxZ));
        }

        /// <summary>
        /// A rectangle of samples, all of them counted as changed.
        /// </summary>
        /// <remarks>
        /// For callers that really did change a whole block — loading a saved island, say.
        /// A brush should use <see cref="WithSample"/> instead, so the circle it actually drew is
        /// what gets rebuilt.
        /// </remarks>
        public static DirtyRegion FromSamples(int minX, int minZ, int maxX, int maxZ)
        {
            if (minX > maxX || minZ > maxZ) return Empty;
            var (xFrom, xTo) = ChunkSpan(minX, maxX);
            var (zFrom, zTo) = ChunkSpan(minZ, maxZ);
            var chunks = 0UL;
            for (var cz = zFrom; cz <= zTo; cz++)
            for (var cx = xFrom; cx <= xTo; cx++)
                chunks |= 1UL << (cz * IslandConstants.ChunksPerSide + cx);
            return new DirtyRegion(chunks, minX, minZ, maxX, maxZ);
        }

        /// <summary>
        /// The inclusive range of chunk indices covering a span of samples.
        /// </summary>
        /// <remarks>
        /// Chunk <c>c</c> covers samples <c>c*Q</c> to <c>c*Q+Q</c> inclusive, where Q is the
        /// quads per side. The ends overlap by one sample: that shared border is what lets two
        /// chunks agree exactly along their seam.
        /// <para>
        /// Public because the chunk renderer needs the same mapping, and two copies of this
        /// arithmetic would be two chances to get the seam wrong.
        /// </para>
        /// </remarks>
        public static (int From, int To) ChunkSpan(int minSample, int maxSample)
        {
            const int quads = IslandConstants.ChunkQuadsPerSide;
            const int last = IslandConstants.ChunksPerSide - 1;
            var from = minSample <= 0 ? 0 : (minSample - 1) / quads;
            var to = maxSample / quads;
            if (from < 0) from = 0;
            if (to > last) to = last;
            if (to < from) to = from;
            return (from, to);
        }

        public bool Equals(DirtyRegion other) =>
            _chunks == other._chunks &&
            (IsEmpty || (MinX == other.MinX && MinZ == other.MinZ && MaxX == other.MaxX && MaxZ == other.MaxZ));

        public override bool Equals(object obj) => obj is DirtyRegion other && Equals(other);

        public override int GetHashCode() => IsEmpty ? 0 : _chunks.GetHashCode() * 397 ^ MinX;

        public override string ToString() =>
            IsEmpty ? "DirtyRegion(empty)"
                    : $"DirtyRegion({ChunkCount} chunks, samples x {MinX}..{MaxX}, z {MinZ}..{MaxZ})";
    }
}
