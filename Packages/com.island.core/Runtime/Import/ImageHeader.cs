using System;
using System.Collections.Generic;
using Island.Core.StreamBox;   // Json: general helpers that happen to live there
using Newtonsoft.Json.Linq;

namespace Island.Core.Import
{
    /// <summary>
    /// Reads a texture's dimensions out of its first few bytes, without decoding it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A 16,384 × 16,384 PNG is about a gigabyte once decoded and a few hundred kilobytes on disk,
    /// so file size is no guide at all to what a texture costs. The dimensions are in the header,
    /// which is why this exists: the decision has to be made before anything decodes, because a
    /// decoder asked to open that image simply takes the memory.
    /// </para>
    /// <para>
    /// The glTF <c>mimeType</c> is not trusted. It is a string in a file the player may have
    /// written; the magic bytes are what the decoder will actually act on, so the magic bytes are
    /// what this reads. A mismatch between the two is itself a refusal.
    /// </para>
    /// </remarks>
    internal static class ImageHeader
    {
        internal readonly struct Measurement
        {
            internal Measurement(int width, int height, string mimeType, string refusal = null)
            {
                Width = width;
                Height = height;
                MimeType = mimeType;
                Refusal = refusal;
            }

            internal int Width { get; }
            internal int Height { get; }
            internal string MimeType { get; }
            internal string Refusal { get; }

            internal static Measurement No(string refusal) => new Measurement(0, 0, null, refusal);
        }

        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static readonly byte[] Ktx2Magic =
            { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A };

        internal static Measurement Measure(JObject image, JObject body, byte[] file,
                                            int binAt, int binLength, ImportLimits limits)
        {
            var view = Json.OptionalInt(image, "bufferView");
            if (!view.HasValue)
                return Measurement.No("This model has a texture with no data attached to it.");

            var views = body?["bufferViews"] as JArray;
            if (views == null || view.Value < 0 || view.Value >= views.Count)
                return Measurement.No("This model refers to texture data that is not in it.");

            if (!(views[view.Value] is JObject bufferView))
                return Measurement.No("This model's texture data is described wrongly.");

            var offset = Json.OptionalInt(bufferView, "byteOffset") ?? 0;
            var length = Json.OptionalInt(bufferView, "byteLength") ?? 0;

            // Every bound checked against the bytes that are really there. A file may claim any
            // offset and length it likes; honouring one would be a read outside the buffer.
            if (binAt < 0) return Measurement.No("This model has textures but no data block.");
            if (offset < 0 || length <= 0)
                return Measurement.No("This model describes a texture of an impossible size.");
            if ((long)offset + length > binLength)
                return Measurement.No("This model's texture data runs past the end of the file.");

            var at = binAt + offset;
            var declared = Json.OptionalString(image, "mimeType");

            var measured = Read(file, at, length);
            if (measured.Refusal != null) return measured;

            var allowed = limits.AllowedImageTypes;
            if (allowed != null && allowed.Count > 0 && !Contains(allowed, measured.MimeType))
            {
                return Measurement.No(
                    $"This model has a {Describe(measured.MimeType)} texture, which Islandia does "
                    + "not read. PNG or JPEG work.");
            }

            if (declared != null && declared != measured.MimeType)
            {
                // The decoder acts on the bytes, not the label. A file whose label disagrees with
                // its content is either broken or trying something.
                return Measurement.No(
                    $"This model has a texture labelled {declared} that is really "
                    + $"{Describe(measured.MimeType)}.");
            }

            return measured;
        }

        /// <summary>Dimensions from the magic bytes, or a refusal.</summary>
        private static Measurement Read(byte[] file, int at, int length)
        {
            if (Starts(file, at, length, PngMagic)) return Png(file, at, length);
            if (Starts(file, at, length, Ktx2Magic)) return Ktx2(file, at, length);
            if (length >= 3 && file[at] == 0xFF && file[at + 1] == 0xD8 && file[at + 2] == 0xFF)
                return Jpeg(file, at, length);

            return Measurement.No("This model has a texture in a format Islandia does not read. "
                                  + "PNG or JPEG work.");
        }

        /// <summary>PNG: IHDR is the first chunk and holds the size at a fixed place.</summary>
        private static Measurement Png(byte[] file, int at, int length)
        {
            // 8 magic + 4 chunk length + 4 "IHDR" + 4 width + 4 height
            if (length < 24) return Measurement.No("This model has a truncated PNG texture.");
            if (file[at + 12] != 'I' || file[at + 13] != 'H' || file[at + 14] != 'D' || file[at + 15] != 'R')
                return Measurement.No("This model has a PNG texture that does not start properly.");

            var width = BigEndian(file, at + 16);
            var height = BigEndian(file, at + 20);
            return Checked(width, height, "image/png");
        }

        /// <summary>
        /// JPEG: walk the markers to a start-of-frame, which carries the size.
        /// </summary>
        /// <remarks>
        /// The only format here that needs walking, because the frame header sits after any number
        /// of metadata segments. Bounded by the data present and by a segment count, so a file of
        /// nothing but empty markers cannot spin.
        /// </remarks>
        private static Measurement Jpeg(byte[] file, int at, int length)
        {
            var end = at + length;
            var cursor = at + 2;
            var segments = 0;

            while (cursor + 4 <= end)
            {
                if (file[cursor] != 0xFF) return Measurement.No("This model has a damaged JPEG texture.");
                if (++segments > 1024) return Measurement.No("This model has a damaged JPEG texture.");

                var marker = file[cursor + 1];

                // Standalone markers carry no length.
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    cursor += 2;
                    continue;
                }

                var segmentLength = (file[cursor + 2] << 8) | file[cursor + 3];
                if (segmentLength < 2) return Measurement.No("This model has a damaged JPEG texture.");

                // Any start-of-frame marker: baseline, progressive, and the arithmetic and
                // lossless variants in between. Not DHT, DAC or the restart markers.
                var isFrame = marker >= 0xC0 && marker <= 0xCF
                              && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;

                if (isFrame)
                {
                    if (cursor + 9 > end) return Measurement.No("This model has a truncated JPEG texture.");
                    var height = (file[cursor + 5] << 8) | file[cursor + 6];
                    var width = (file[cursor + 7] << 8) | file[cursor + 8];
                    return Checked(width, height, "image/jpeg");
                }

                cursor += 2 + segmentLength;
            }

            return Measurement.No("This model has a JPEG texture with no picture in it.");
        }

        /// <summary>KTX2: a fixed header, little-endian, size at a known offset.</summary>
        private static Measurement Ktx2(byte[] file, int at, int length)
        {
            // 12 magic + 4 format + 4 type size + 4 width + 4 height
            if (length < 28) return Measurement.No("This model has a truncated KTX2 texture.");
            var width = (int)LittleEndian(file, at + 20);
            var height = (int)LittleEndian(file, at + 24);
            return Checked(width, height, "image/ktx2");
        }

        private static Measurement Checked(long width, long height, string mimeType)
        {
            if (width <= 0 || height <= 0)
                return Measurement.No("This model has a texture with no size.");

            // Past this, the numbers stop being plausible and start being an attempt. 65,536 is
            // far beyond anything any headset will ever sample.
            if (width > 65_536 || height > 65_536)
                return Measurement.No($"This model claims a {width} × {height} texture, which is not real.");

            return new Measurement((int)width, (int)height, mimeType);
        }

        private static bool Starts(byte[] file, int at, int length, byte[] magic)
        {
            if (length < magic.Length) return false;
            for (var i = 0; i < magic.Length; i++) if (file[at + i] != magic[i]) return false;
            return true;
        }

        private static bool Contains(IReadOnlyList<string> allowed, string mimeType)
        {
            for (var i = 0; i < allowed.Count; i++)
                if (string.Equals(allowed[i], mimeType, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Describe(string mimeType) =>
            mimeType == null ? "unrecognised" : mimeType.Replace("image/", "").ToUpperInvariant();

        private static long BigEndian(byte[] bytes, int at) =>
            ((long)bytes[at] << 24) | ((long)bytes[at + 1] << 16) | ((long)bytes[at + 2] << 8) | bytes[at + 3];

        private static uint LittleEndian(byte[] bytes, int at) =>
            (uint)(bytes[at] | bytes[at + 1] << 8 | bytes[at + 2] << 16 | bytes[at + 3] << 24);
    }
}
