using System;
using System.IO;
using Island.Core.Save;
using Island.Core.Terrain;

namespace Island.Core.World
{
    /// <summary>Which way the track leaves the island: the direction the station faces.</summary>
    public enum TrackDirection
    {
        /// <summary>Towards +X.</summary>
        East = 0,
        /// <summary>Towards +Z.</summary>
        North = 1,
        /// <summary>Towards −X.</summary>
        West = 2,
        /// <summary>Towards −Z.</summary>
        South = 3,
    }

    /// <summary>A rectangle of heightfield samples, both corners included.</summary>
    public readonly struct GridRect
    {
        public GridRect(int minX, int minZ, int maxX, int maxZ)
        {
            MinX = Math.Min(minX, maxX);
            MaxX = Math.Max(minX, maxX);
            MinZ = Math.Min(minZ, maxZ);
            MaxZ = Math.Max(minZ, maxZ);
        }

        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }

        public bool Contains(int x, int z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        public bool Intersects(GridRect other) =>
            MinX <= other.MaxX && MaxX >= other.MinX && MinZ <= other.MaxZ && MaxZ >= other.MinZ;

        /// <summary>Whether a world-space box, in metres, overlaps this rectangle's ground.</summary>
        public bool Overlaps(float minX, float minZ, float maxX, float maxZ) =>
            minX <= Heightfield.WorldXOf(MaxX) && maxX >= Heightfield.WorldXOf(MinX) &&
            minZ <= Heightfield.WorldZOf(MaxZ) && maxZ >= Heightfield.WorldZOf(MinZ);

        public override string ToString() => $"[{MinX}..{MaxX}] x [{MinZ}..{MaxZ}]";
    }

    /// <summary>
    /// Where an island's station stands and the strip of water its track leaves along.
    /// </summary>
    /// <remarks>
    /// Both are reserved (ADR-24): no brush changes their ground (<see cref="LockInto"/>) and
    /// nothing may be placed on them (<see cref="Permits"/>). The site is saved with the island
    /// rather than recomputed from its seed, so a later change to how islands are generated can
    /// never move a station out from under the terrain built around it.
    /// </remarks>
    public sealed class StationSite
    {
        public StationSite(TrackDirection direction, GridRect footprint, GridRect corridor)
        {
            Direction = direction;
            Footprint = footprint;
            Corridor = corridor;
        }

        public TrackDirection Direction { get; }

        /// <summary>The station and its platform: levelled, dry, and owned by the station.</summary>
        public GridRect Footprint { get; }

        /// <summary>From the station's seaward edge to the island's edge, over open water.</summary>
        public GridRect Corridor { get; }

        /// <summary>
        /// The view down the line: the footprint's width, from its seaward edge to the island's
        /// edge. Open water when the island is made; wider than the corridor, and not reserved.
        /// </summary>
        public GridRect Front =>
            Direction == TrackDirection.East || Direction == TrackDirection.West
                ? new GridRect(Corridor.MinX, Footprint.MinZ, Corridor.MaxX, Footprint.MaxZ)
                : new GridRect(Footprint.MinX, Corridor.MinZ, Footprint.MaxX, Corridor.MaxZ);

        public bool IsReserved(int gridX, int gridZ) =>
            Footprint.Contains(gridX, gridZ) || Corridor.Contains(gridX, gridZ);

        /// <summary>
        /// Whether something whose footprint is this world-space box, in metres, may be placed.
        /// Every placement path asks this; anything touching the station or its track is refused.
        /// </summary>
        public bool Permits(float minX, float minZ, float maxX, float maxZ) =>
            !Footprint.Overlaps(minX, minZ, maxX, maxZ) && !Corridor.Overlaps(minX, minZ, maxX, maxZ);

        /// <summary>Stops every brush from changing the reserved ground.</summary>
        public void LockInto(Heightfield field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            field.Lock(Footprint.MinX, Footprint.MinZ, Footprint.MaxX, Footprint.MaxZ);
            field.Lock(Corridor.MinX, Corridor.MinZ, Corridor.MaxX, Corridor.MaxZ);
        }

        // ---- the station's own frame ----------------------------------------------------------

        private const int Last = IslandConstants.HeightfieldResolution - 1;

        /// <summary>
        /// A sample from the station's frame: <paramref name="inland"/> samples in from the
        /// island's edge on the side the track leaves by, and <paramref name="across"/> along that edge.
        /// </summary>
        internal static (int X, int Z) ToGrid(TrackDirection direction, int inland, int across)
        {
            switch (direction)
            {
                case TrackDirection.East: return (Last - inland, across);
                case TrackDirection.West: return (inland, across);
                case TrackDirection.North: return (across, Last - inland);
                default: return (across, inland);
            }
        }

        /// <summary>How far in from the seaward edge a sample is, in the station's frame.</summary>
        internal static int InlandOf(TrackDirection direction, int x, int z)
        {
            switch (direction)
            {
                case TrackDirection.East: return Last - x;
                case TrackDirection.West: return x;
                case TrackDirection.North: return Last - z;
                default: return z;
            }
        }

        internal static GridRect RectInFrame(TrackDirection direction, int inlandFrom, int inlandTo,
                                             int acrossFrom, int acrossTo)
        {
            var (x0, z0) = ToGrid(direction, inlandFrom, acrossFrom);
            var (x1, z1) = ToGrid(direction, inlandTo, acrossTo);
            return new GridRect(x0, z0, x1, z1);
        }
    }

    /// <summary>Saves an island's <see cref="StationSite"/>, and locks it into the ground on reading.</summary>
    public sealed class StationSaveSection : ISaveSection
    {
        public const string Id = "STAN";
        public const int Version = 1;

        private readonly Heightfield _lockInto;

        /// <param name="site">The site to write; null when reading.</param>
        /// <param name="lockInto">
        /// The island's ground, locked as soon as the site is read, so no load path can leave a
        /// station unprotected by forgetting to.
        /// </param>
        public StationSaveSection(StationSite site, Heightfield lockInto = null)
        {
            Site = site;
            _lockInto = lockInto;
        }

        public StationSite Site { get; private set; }

        public string SectionId => Id;
        public int SectionVersion => Version;

        public byte[] Write()
        {
            if (Site == null) throw new InvalidOperationException("There is no station to save.");
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                writer.Write((int)Site.Direction);
                foreach (var rect in new[] { Site.Footprint, Site.Corridor })
                {
                    writer.Write(rect.MinX);
                    writer.Write(rect.MinZ);
                    writer.Write(rect.MaxX);
                    writer.Write(rect.MaxZ);
                }
                writer.Flush();
                return buffer.ToArray();
            }
        }

        public void Read(byte[] payload, int version)
        {
            if (version != Version)
                throw new SaveFormatException($"Section '{Id}' is version {version}; this build reads {Version}.");
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(payload, false)))
                {
                    var direction = reader.ReadInt32();
                    if (direction < 0 || direction > 3)
                        throw new SaveFormatException($"The station faces direction {direction}, which does not exist.");
                    var footprint = ReadRect(reader);
                    var corridor = ReadRect(reader);
                    Site = new StationSite((TrackDirection)direction, footprint, corridor);
                    if (_lockInto != null) Site.LockInto(_lockInto);
                }
            }
            catch (EndOfStreamException)
            {
                throw new SaveFormatException($"Section '{Id}' is cut short.");
            }
        }

        private static GridRect ReadRect(BinaryReader reader)
        {
            var rect = new GridRect(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            if (!Heightfield.InRange(rect.MinX, rect.MinZ) || !Heightfield.InRange(rect.MaxX, rect.MaxZ))
                throw new SaveFormatException($"The station's reservation {rect} lies off the island.");
            return rect;
        }
    }
}
