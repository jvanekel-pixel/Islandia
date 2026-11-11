using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Island.Core.Tests.Import
{
    /// <summary>
    /// Builds GLB files byte by byte, including ones no exporter would produce.
    /// </summary>
    /// <remarks>
    /// The gatekeeper's job is to refuse hostile files, so the tests have to be able to write
    /// them: a chunk claiming four billion bytes, a node tree that loops, a PNG header declaring
    /// sixteen thousand pixels. A fixture corpus of real exported models could not cover any of
    /// that, and checking in deliberately malformed binaries would be worse — unreadable in a
    /// diff, and indistinguishable from a mistake.
    /// </remarks>
    internal sealed class GlbBuilder
    {
        private const uint Magic = 0x46546C67;
        private const uint JsonChunk = 0x4E4F534A;
        private const uint BinChunk = 0x004E4942;

        private readonly List<byte> _binary = new List<byte>();

        internal JObject Gltf { get; } = new JObject
        {
            ["asset"] = new JObject { ["version"] = "2.0" },
            ["buffers"] = new JArray(),
            ["bufferViews"] = new JArray(),
            ["accessors"] = new JArray(),
            ["meshes"] = new JArray(),
            ["nodes"] = new JArray(),
            ["images"] = new JArray(),
        };

        internal uint VersionOverride { get; set; } = 2;
        internal uint? DeclaredLengthOverride { get; set; }
        internal uint? JsonChunkLengthOverride { get; set; }
        internal bool PutBinaryChunkFirst { get; set; }
        internal bool OmitJsonChunk { get; set; }
        internal uint MagicOverride { get; set; } = Magic;

        // ---- building a plausible model --------------------------------------------------------

        /// <summary>Adds an accessor with a given element count and returns its index.</summary>
        internal int Accessor(int count)
        {
            var accessors = (JArray)Gltf["accessors"];
            accessors.Add(new JObject { ["count"] = count, ["componentType"] = 5123, ["type"] = "SCALAR" });
            return accessors.Count - 1;
        }

        /// <summary>A mesh of one triangle-mode primitive with the given triangle count.</summary>
        internal int Mesh(int triangles, int primitives = 1)
        {
            var list = new JArray();
            for (var i = 0; i < primitives; i++)
            {
                list.Add(new JObject
                {
                    ["mode"] = 4,
                    ["indices"] = Accessor(triangles * 3),
                    ["attributes"] = new JObject { ["POSITION"] = Accessor(triangles * 3) },
                });
            }

            var meshes = (JArray)Gltf["meshes"];
            meshes.Add(new JObject { ["primitives"] = list });
            return meshes.Count - 1;
        }

        internal int Node(int? mesh = null, IEnumerable<int> children = null)
        {
            var node = new JObject();
            if (mesh.HasValue) node["mesh"] = mesh.Value;
            if (children != null)
            {
                var list = new JArray();
                foreach (var child in children) list.Add(child);
                node["children"] = list;
            }

            var nodes = (JArray)Gltf["nodes"];
            nodes.Add(node);
            return nodes.Count - 1;
        }

        /// <summary>Puts bytes in the binary chunk and adds a bufferView over them.</summary>
        internal int BufferView(byte[] bytes)
        {
            var offset = _binary.Count;
            _binary.AddRange(bytes);
            while (_binary.Count % 4 != 0) _binary.Add(0);

            var views = (JArray)Gltf["bufferViews"];
            views.Add(new JObject
            {
                ["buffer"] = 0, ["byteOffset"] = offset, ["byteLength"] = bytes.Length,
            });
            return views.Count - 1;
        }

        internal int Image(byte[] header, string mimeType)
        {
            var images = (JArray)Gltf["images"];
            var image = new JObject { ["bufferView"] = BufferView(header) };
            if (mimeType != null) image["mimeType"] = mimeType;
            images.Add(image);
            return images.Count - 1;
        }

        /// <summary>A PNG header declaring a size. No pixels: the gatekeeper reads no pixels.</summary>
        internal static byte[] Png(int width, int height)
        {
            var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            bytes.AddRange(BigEndian(13));
            bytes.AddRange(Encoding.ASCII.GetBytes("IHDR"));
            bytes.AddRange(BigEndian((uint)width));
            bytes.AddRange(BigEndian((uint)height));
            bytes.AddRange(new byte[] { 8, 6, 0, 0, 0 });   // depth, colour type, the rest
            return bytes.ToArray();
        }

        /// <summary>A JPEG with one metadata segment before the frame, as real ones have.</summary>
        internal static byte[] Jpeg(int width, int height)
        {
            var bytes = new List<byte> { 0xFF, 0xD8 };

            // An APP0 segment, so the walk has something to step over.
            bytes.AddRange(new byte[] { 0xFF, 0xE0, 0x00, 0x10 });
            bytes.AddRange(Encoding.ASCII.GetBytes("JFIF\0"));
            bytes.AddRange(new byte[9]);

            // SOF0: length, precision, height, width, components.
            bytes.AddRange(new byte[] { 0xFF, 0xC0, 0x00, 0x11, 0x08 });
            bytes.Add((byte)(height >> 8)); bytes.Add((byte)(height & 0xFF));
            bytes.Add((byte)(width >> 8)); bytes.Add((byte)(width & 0xFF));
            bytes.Add(3);
            bytes.AddRange(new byte[9]);
            return bytes.ToArray();
        }

        internal static byte[] Ktx2(int width, int height)
        {
            var bytes = new List<byte>
                { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A };
            bytes.AddRange(LittleEndian(0));          // vkFormat
            bytes.AddRange(LittleEndian(1));          // typeSize
            bytes.AddRange(LittleEndian((uint)width));
            bytes.AddRange(LittleEndian((uint)height));
            bytes.AddRange(new byte[16]);
            return bytes.ToArray();
        }

        // ---- writing it out --------------------------------------------------------------------

        internal byte[] Build()
        {
            if (_binary.Count > 0 && ((JArray)Gltf["buffers"]).Count == 0)
                ((JArray)Gltf["buffers"]).Add(new JObject { ["byteLength"] = _binary.Count });

            var json = Encoding.UTF8.GetBytes(Gltf.ToString(Newtonsoft.Json.Formatting.None));
            var jsonPadded = Pad(json, 0x20);
            var binPadded = Pad(_binary.ToArray(), 0x00);

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(MagicOverride);
                writer.Write(VersionOverride);

                var total = 12
                    + (OmitJsonChunk ? 0 : 8 + jsonPadded.Length)
                    + (binPadded.Length > 0 ? 8 + binPadded.Length : 0);
                writer.Write(DeclaredLengthOverride ?? (uint)total);

                Action writeJson = () =>
                {
                    if (OmitJsonChunk) return;
                    writer.Write(JsonChunkLengthOverride ?? (uint)jsonPadded.Length);
                    writer.Write(JsonChunk);
                    writer.Write(jsonPadded);
                };

                Action writeBin = () =>
                {
                    if (binPadded.Length == 0) return;
                    writer.Write((uint)binPadded.Length);
                    writer.Write(BinChunk);
                    writer.Write(binPadded);
                };

                if (PutBinaryChunkFirst) { writeBin(); writeJson(); }
                else { writeJson(); writeBin(); }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] Pad(byte[] bytes, byte with)
        {
            if (bytes.Length % 4 == 0) return bytes;
            var padded = new byte[bytes.Length + (4 - bytes.Length % 4)];
            Array.Copy(bytes, padded, bytes.Length);
            for (var i = bytes.Length; i < padded.Length; i++) padded[i] = with;
            return padded;
        }

        private static byte[] BigEndian(uint value) => new[]
            { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

        private static byte[] LittleEndian(uint value) => new[]
            { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };
    }
}
