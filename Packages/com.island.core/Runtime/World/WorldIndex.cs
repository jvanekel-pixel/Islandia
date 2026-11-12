using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Island.Core.Save;

namespace Island.Core.World
{
    /// <summary>One island as the world index knows it: enough to list it, never its contents.</summary>
    public sealed class IslandEntry
    {
        internal IslandEntry(long id, string name, ulong seed, string theme, long createdAtUnixMs, string thumbnail)
        {
            Id = id;
            Name = name;
            Seed = seed;
            Theme = theme;
            CreatedAtUnixMs = createdAtUnixMs;
            Thumbnail = thumbnail;
        }

        /// <summary>Issued once and never reused, even after the island is deleted.</summary>
        public long Id { get; }
        public string Name { get; internal set; }
        public ulong Seed { get; }
        public string Theme { get; }
        public long CreatedAtUnixMs { get; }

        /// <summary>Path of the picture taken when the island was last left, relative to the world; null until then.</summary>
        public string Thumbnail { get; internal set; }
    }

    /// <summary>
    /// The list of every island in the world (ADR-24). Small by design: it is read at every
    /// start and shown on the departure board, and it never holds an island's contents.
    /// </summary>
    public sealed class WorldIndex
    {
        public const string SectionId = "WRLD";
        public const int SectionVersion = 1;

        /// <summary>Longest island name, in characters: enough for a departure board, not a paragraph.</summary>
        public const int MaxNameLength = 40;

        private readonly List<IslandEntry> _islands = new List<IslandEntry>();

        public WorldIndex(ulong worldSeed) => WorldSeed = worldSeed;

        /// <summary>Where the seeds of islands made without one of their own come from.</summary>
        public ulong WorldSeed { get; }

        /// <summary>The id the next island will get. Only ever goes up.</summary>
        public long NextIslandId { get; private set; } = 1;

        /// <summary>In the order they were made. There is no upper bound.</summary>
        public IReadOnlyList<IslandEntry> Islands => _islands;

        public IslandEntry Find(long id)
        {
            foreach (var entry in _islands)
                if (entry.Id == id) return entry;
            return null;
        }

        /// <summary>The seed an island gets when nobody chose one: derived from the world's and the island's id.</summary>
        public ulong SeedFor(long id) => SeedStream.Mix(WorldSeed ^ SeedStream.Mix((ulong)id));

        public IslandEntry Add(string name, ulong seed, string theme, long createdAtUnixMs)
        {
            var entry = new IslandEntry(NextIslandId, CheckName(name), seed, theme ?? "", createdAtUnixMs, null);
            NextIslandId++;
            _islands.Add(entry);
            return entry;
        }

        public bool Remove(long id) => _islands.RemoveAll(e => e.Id == id) > 0;

        public void Rename(long id, string name) => Require(id).Name = CheckName(name);

        public void SetThumbnail(long id, string relativePath) => Require(id).Thumbnail = relativePath;

        private IslandEntry Require(long id) =>
            Find(id) ?? throw new ArgumentException($"There is no island {id} in this world.", nameof(id));

        private static string CheckName(string name)
        {
            var trimmed = (name ?? "").Trim();
            if (trimmed.Length == 0) throw new ArgumentException("An island needs a name.", nameof(name));
            return trimmed.Length > MaxNameLength ? trimmed.Substring(0, MaxNameLength) : trimmed;
        }

        // ---- bytes -----------------------------------------------------------------------------------

        public SaveSection ToSection()
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer, Encoding.UTF8))
            {
                writer.Write(WorldSeed);
                writer.Write(NextIslandId);
                writer.Write(_islands.Count);
                foreach (var entry in _islands)
                {
                    writer.Write(entry.Id);
                    writer.Write(entry.Name);
                    writer.Write(entry.Seed);
                    writer.Write(entry.Theme);
                    writer.Write(entry.CreatedAtUnixMs);
                    writer.Write(entry.Thumbnail != null);
                    if (entry.Thumbnail != null) writer.Write(entry.Thumbnail);
                }
                writer.Flush();
                return new SaveSection(SectionId, SectionVersion, buffer.ToArray());
            }
        }

        public static WorldIndex FromSection(SaveSection section)
        {
            if (section == null) throw new SaveFormatException("The world index has no list of islands in it.");
            if (section.Version != SectionVersion)
                throw new SaveFormatException($"Section '{SectionId}' is version {section.Version}; this build reads {SectionVersion}.");
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(section.Payload, false), Encoding.UTF8))
                {
                    var index = new WorldIndex(reader.ReadUInt64()) { NextIslandId = reader.ReadInt64() };
                    var count = reader.ReadInt32();
                    if (count < 0) throw new SaveFormatException($"The world index claims {count} islands.");
                    var seen = new HashSet<long>();
                    for (var i = 0; i < count; i++)
                    {
                        var id = reader.ReadInt64();
                        var name = reader.ReadString();
                        var seed = reader.ReadUInt64();
                        var theme = reader.ReadString();
                        var created = reader.ReadInt64();
                        var thumbnail = reader.ReadBoolean() ? reader.ReadString() : null;
                        if (id < 1 || id >= index.NextIslandId || !seen.Add(id))
                            throw new SaveFormatException($"The world index lists island {id}, which cannot be right.");
                        index._islands.Add(new IslandEntry(id, name, seed, theme, created, thumbnail));
                    }
                    return index;
                }
            }
            catch (EndOfStreamException)
            {
                throw new SaveFormatException("The world index is cut short.");
            }
        }
    }
}
