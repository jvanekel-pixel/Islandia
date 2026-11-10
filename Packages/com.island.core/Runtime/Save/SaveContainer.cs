using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Island.Core.Save
{
    /// <summary>
    /// A save file's bytes: a header, then any number of independent sections.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Layout, little-endian throughout:
    /// </para>
    /// <code>
    ///   magic      8 bytes   "ISLANDIA"
    ///   format     int32     the container format, not the game's
    ///   saved at   int64     Unix milliseconds, for showing a date in a slot list
    ///   sections   int32     how many follow
    ///   headerCrc  uint32    of the 24 bytes above
    ///   per section:
    ///     crc32    uint32    of the block below, header fields included
    ///     length   int32     bytes in the block
    ///     block:
    ///       id       4 bytes   ASCII
    ///       version  int32     this section's own version
    ///       flags    int32     bit 0: payload is deflate-compressed
    ///       stored   int32     payload bytes as written
    ///       raw      int32     payload bytes after decompression
    ///       payload  stored bytes
    /// </code>
    /// <para>
    /// Every byte is covered by a checksum, including the framing. An earlier version checksummed
    /// only the payloads, which left the dangerous case uncovered: a flipped bit in the section
    /// count does not fail to parse, it quietly yields an island missing its pets. Silent loss is
    /// worse than a refusal.
    /// </para>
    /// <para>
    /// The checksum is per section rather than per file so damage can be named — "the foliage
    /// section is corrupt" — instead of condemning an island someone spent a weekend on.
    /// </para>
    /// </remarks>
    public sealed class SaveContainer
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ISLANDIA");

        /// <summary>The container format. Only changes if the framing above changes.</summary>
        public const int CurrentFormat = 1;

        /// <summary>Below this, compressing costs more in overhead than it saves.</summary>
        private const int CompressAbove = 512;

        private readonly List<SaveSection> _sections = new List<SaveSection>();

        public SaveContainer() : this(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) { }

        public SaveContainer(long savedAtUnixMs) => SavedAtUnixMs = savedAtUnixMs;

        public long SavedAtUnixMs { get; private set; }

        public int Format { get; private set; } = CurrentFormat;

        public IReadOnlyList<SaveSection> Sections => _sections;

        public SaveSection Find(string id)
        {
            foreach (var section in _sections)
                if (section.Id == id) return section;
            return null;
        }

        /// <summary>Adds a section, replacing any existing one with the same id.</summary>
        public void Put(SaveSection section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            for (var i = 0; i < _sections.Count; i++)
            {
                if (_sections[i].Id != section.Id) continue;
                _sections[i] = section;
                return;
            }
            _sections.Add(section);
        }

        public void Put(ISaveSection subsystem)
        {
            if (subsystem == null) throw new ArgumentNullException(nameof(subsystem));
            Put(new SaveSection(subsystem.SectionId, subsystem.SectionVersion, subsystem.Write()));
        }

        /// <summary>
        /// Hands a section to its subsystem, if the file has one.
        /// </summary>
        /// <returns>False when this save predates the subsystem, which is not an error.</returns>
        public bool ReadInto(ISaveSection subsystem)
        {
            if (subsystem == null) throw new ArgumentNullException(nameof(subsystem));
            var section = Find(subsystem.SectionId);
            if (section == null) return false;
            subsystem.Read(section.Payload, section.Version);
            return true;
        }

        // ---- bytes ---------------------------------------------------------------------------------

        public byte[] ToBytes()
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer, Encoding.ASCII))
            {
                using (var header = new MemoryStream())
                using (var headerWriter = new BinaryWriter(header, Encoding.ASCII))
                {
                    headerWriter.Write(Magic);
                    headerWriter.Write(CurrentFormat);
                    headerWriter.Write(SavedAtUnixMs);
                    headerWriter.Write(_sections.Count);
                    headerWriter.Flush();
                    var headerBytes = header.ToArray();
                    writer.Write(headerBytes);
                    writer.Write(Crc32.Compute(headerBytes));
                }

                foreach (var section in _sections)
                {
                    var raw = section.Payload;
                    var compressed = raw.Length > CompressAbove;
                    var stored = compressed ? Deflate(raw) : raw;

                    // Compression that made it bigger — small or already-packed data — is not
                    // worth the decompression on load.
                    if (compressed && stored.Length >= raw.Length)
                    {
                        compressed = false;
                        stored = raw;
                    }

                    byte[] block;
                    using (var blockBuffer = new MemoryStream())
                    using (var blockWriter = new BinaryWriter(blockBuffer, Encoding.ASCII))
                    {
                        blockWriter.Write(Encoding.ASCII.GetBytes(section.Id));
                        blockWriter.Write(section.Version);
                        blockWriter.Write(compressed ? 1 : 0);
                        blockWriter.Write(stored.Length);
                        blockWriter.Write(raw.Length);
                        blockWriter.Write(stored);
                        blockWriter.Flush();
                        block = blockBuffer.ToArray();
                    }

                    writer.Write(Crc32.Compute(block));
                    writer.Write(block.Length);
                    writer.Write(block);
                }

                writer.Flush();
                return buffer.ToArray();
            }
        }

        public static SaveContainer FromBytes(byte[] bytes)
        {
            const int headerLength = 8 + 4 + 8 + 4;
            if (bytes == null || bytes.Length < headerLength + 4)
                throw new SaveFormatException("The file is too short to be a save.");

            for (var i = 0; i < Magic.Length; i++)
            {
                if (bytes[i] != Magic[i]) throw new SaveFormatException("This is not an Islandia save file.");
            }

            using (var buffer = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(buffer, Encoding.ASCII))
            {
                reader.ReadBytes(Magic.Length);
                var format = reader.ReadInt32();
                var savedAt = reader.ReadInt64();
                var count = reader.ReadInt32();
                var headerCrc = reader.ReadUInt32();

                if (Crc32.Compute(bytes, 0, headerLength) != headerCrc)
                {
                    throw new SaveFormatException(
                        "The file's header is corrupt, so there is no telling what it contains.");
                }
                if (format < 1 || format > CurrentFormat)
                {
                    throw new SaveFormatException(
                        $"This save is in format {format}; this build understands up to {CurrentFormat}. " +
                        "It was probably written by a newer version of the game.");
                }
                if (count < 0 || count > 4096) throw new SaveFormatException($"The file claims {count} sections.");

                var container = new SaveContainer(savedAt) { Format = format };

                for (var i = 0; i < count; i++)
                {
                    uint expected;
                    int blockLength;
                    try
                    {
                        expected = reader.ReadUInt32();
                        blockLength = reader.ReadInt32();
                    }
                    catch (EndOfStreamException)
                    {
                        throw new SaveFormatException($"The file ends before section {i + 1} of {count}.");
                    }

                    if (blockLength < SaveSection.IdLength + 16 || blockLength > bytes.Length)
                        throw new SaveFormatException($"Section {i + 1} claims an impossible length.");

                    var block = reader.ReadBytes(blockLength);
                    if (block.Length != blockLength)
                        throw new SaveFormatException($"Section {i + 1} is cut short; the file is incomplete.");

                    var actual = Crc32.Compute(block);
                    if (actual != expected)
                    {
                        // Name it where we can. The id lives inside the block we cannot trust, so
                        // it is only worth quoting when it still looks like an id.
                        var id = Encoding.ASCII.GetString(block, 0, SaveSection.IdLength);
                        var readable = true;
                        foreach (var character in id)
                            if (character < 32 || character > 126) readable = false;
                        var which = readable ? $"'{id}'" : $"{i + 1}";
                        throw new SaveFormatException(
                            $"Section {which} is corrupt: its checksum is {actual:x8}, the file says {expected:x8}.");
                    }

                    using (var blockBuffer = new MemoryStream(block, false))
                    using (var blockReader = new BinaryReader(blockBuffer, Encoding.ASCII))
                    {
                        var id = Encoding.ASCII.GetString(blockReader.ReadBytes(SaveSection.IdLength));
                        var version = blockReader.ReadInt32();
                        var flags = blockReader.ReadInt32();
                        var storedLength = blockReader.ReadInt32();
                        var rawLength = blockReader.ReadInt32();

                        if (storedLength < 0 || rawLength < 0 || storedLength > block.Length)
                            throw new SaveFormatException($"Section '{id}' claims an impossible payload length.");

                        var stored = blockReader.ReadBytes(storedLength);
                        var payload = (flags & 1) != 0 ? Inflate(stored, rawLength) : stored;
                        container._sections.Add(new SaveSection(id, version, payload));
                    }
                }

                return container;
            }
        }

        private static byte[] Deflate(byte[] raw)
        {
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                    deflate.Write(raw, 0, raw.Length);
                return output.ToArray();
            }
        }

        private static byte[] Inflate(byte[] stored, int rawLength)
        {
            try
            {
                var result = new byte[rawLength];
                using (var input = new MemoryStream(stored, false))
                using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
                {
                    var read = 0;
                    while (read < rawLength)
                    {
                        var got = inflate.Read(result, read, rawLength - read);
                        if (got <= 0) break;
                        read += got;
                    }
                    if (read != rawLength)
                        throw new SaveFormatException("A compressed section did not unpack to its stated size.");
                }
                return result;
            }
            catch (InvalidDataException error)
            {
                throw new SaveFormatException("A compressed section could not be unpacked: " + error.Message);
            }
        }
    }

    /// <summary>The bytes are not a save file this build can read.</summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message) { }
    }
}
