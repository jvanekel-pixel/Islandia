using System;

namespace Island.Core.Save
{
    /// <summary>
    /// One subsystem's slice of a save file: terrain, foliage, structures, pets, settings.
    /// </summary>
    /// <remarks>
    /// Sections are independent on purpose. Each carries its own version, so the terrain format
    /// can change without touching the pet format; each carries its own checksum, so a damaged
    /// section can be named rather than condemning the whole island; and a section this build has
    /// never heard of is <em>kept</em> rather than dropped. That last point matters more than it
    /// looks: without it, opening an island in an older build and saving it would silently delete
    /// everything the newer build had added.
    /// </remarks>
    public sealed class SaveSection
    {
        /// <summary>Four ASCII characters. Short, fixed width, and readable in a hex dump.</summary>
        public const int IdLength = 4;

        public SaveSection(string id, int version, byte[] payload)
        {
            if (string.IsNullOrEmpty(id) || id.Length != IdLength)
                throw new ArgumentException($"A section id must be exactly {IdLength} characters; got '{id}'.", nameof(id));
            foreach (var character in id)
            {
                if (character < 32 || character > 126)
                    throw new ArgumentException($"A section id must be printable ASCII; got '{id}'.", nameof(id));
            }
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version), "Section versions start at 1.");

            Id = id;
            Version = version;
            Payload = payload ?? new byte[0];
        }

        public string Id { get; }

        /// <summary>This section's own format version, independent of every other section.</summary>
        public int Version { get; }

        public byte[] Payload { get; }

        public override string ToString() => $"{Id} v{Version} ({Payload.Length} bytes)";
    }

    /// <summary>A subsystem that has something to save.</summary>
    public interface ISaveSection
    {
        /// <summary>Four characters, unique across the game.</summary>
        string SectionId { get; }

        /// <summary>The version this build writes. Bumped when the payload's shape changes.</summary>
        int SectionVersion { get; }

        byte[] Write();

        /// <summary>
        /// Reads a section back, given the version it was written at.
        /// </summary>
        /// <remarks>
        /// The version is passed in rather than read from the payload so that migrating an old
        /// save is explicit: a subsystem must decide what to do with an older version, and cannot
        /// accidentally read it as though it were current.
        /// </remarks>
        void Read(byte[] payload, int version);
    }
}
