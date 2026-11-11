using System;
using System.Collections.Generic;
using System.Text;
using Island.Core.StreamBox;   // Json: general helpers that happen to live there
using Newtonsoft.Json.Linq;

namespace Island.Core.Import
{
    /// <summary>What a file turned out to be. Numbers measured, never taken on trust.</summary>
    public sealed class ImportFacts
    {
        public long FileBytes { get; internal set; }
        public int Triangles { get; internal set; }

        /// <summary>Primitives drawn across the scene — the draw calls this model costs.</summary>
        public int Primitives { get; internal set; }

        public int Meshes { get; internal set; }
        public int Nodes { get; internal set; }
        public int Images { get; internal set; }
        public int MaxTextureDimension { get; internal set; }

        /// <summary>Geometry plus textures, with mips. An estimate, and deliberately generous.</summary>
        public long EstimatedBytes { get; internal set; }

        public override string ToString() =>
            $"{Triangles:N0} triangles, {Primitives} primitive(s), {Images} image(s), "
            + $"largest texture {MaxTextureDimension}px, about {EstimatedBytes / (1024 * 1024)} MB";
    }

    /// <summary>Whether a file may be loaded, and why not.</summary>
    public sealed class ImportVerdict
    {
        private ImportVerdict(bool accepted, string reason, ImportFacts facts)
        {
            Accepted = accepted;
            Reason = reason;
            Facts = facts;
        }

        public bool Accepted { get; }

        /// <summary>
        /// Why it was refused, in terms a person can act on. Null when accepted.
        /// </summary>
        /// <remarks>
        /// Shown to the player, so it says what to do about it — "this model has 92,000 triangles
        /// and the limit is 40,000" rather than "validation failed". `RT-09` is that people cannot
        /// tell why a file was rejected.
        /// </remarks>
        public string Reason { get; }

        public ImportFacts Facts { get; }

        internal static ImportVerdict Yes(ImportFacts facts) => new ImportVerdict(true, null, facts);

        internal static ImportVerdict No(string reason, ImportFacts facts = null) =>
            new ImportVerdict(false, reason, facts);
    }

    /// <summary>
    /// Decides whether a GLB file may be loaded, without decoding any of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The headset is the trust boundary</b> (ADR-12). The browser converter checks a file too,
    /// but it runs in the player's own browser and can be bypassed by dropping a file straight
    /// into the imports folder — so this checks everything again and believes nothing the file says
    /// about itself. Every length is validated against the bytes actually present before it is
    /// used, and nothing is ever allocated to a size the file asked for.
    /// </para>
    /// <para>
    /// It reads the container, the JSON and the image headers. It does not decode geometry or
    /// pixels: the point is to decide <em>before</em> handing anything to glTFast, which will
    /// happily allocate whatever it is given.
    /// </para>
    /// <para>
    /// Nothing escapes as an exception. A hostile file is a verdict, not a crash, because the
    /// caller is an import screen and a thrown exception there is a player watching their world
    /// disappear.
    /// </para>
    /// </remarks>
    public static class GlbGatekeeper
    {
        private const uint Magic = 0x46546C67;      // "glTF"
        private const uint JsonChunk = 0x4E4F534A;  // "JSON"
        private const uint BinChunk = 0x004E4942;   // "BIN\0"
        private const int HeaderBytes = 12;
        private const int ChunkHeaderBytes = 8;

        /// <summary>glTF primitive modes. Only the three triangle modes draw triangles.</summary>
        private const int Triangles = 4;
        private const int TriangleStrip = 5;
        private const int TriangleFan = 6;

        /// <summary>Mip chains add roughly a third again to a texture's size.</summary>
        private const double MipFactor = 4.0 / 3.0;

        /// <summary>Bytes per pixel assumed for an uncompressed texture on device.</summary>
        private const int BytesPerPixel = 4;

        public static ImportVerdict Inspect(byte[] file, ImportLimits limits)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));

            try
            {
                return Judge(file, limits);
            }
            catch (Exception error)
            {
                // The last line of defence. Anything that gets here is a bug in this class rather
                // than a known shape of bad file, and the honest thing is to refuse the file and
                // say so plainly rather than let it through or crash the import screen.
                return ImportVerdict.No(
                    $"This file could not be read ({error.GetType().Name}). It may be damaged.");
            }
        }

        private static ImportVerdict Judge(byte[] file, ImportLimits limits)
        {
            if (file == null || file.Length == 0) return ImportVerdict.No("This file is empty.");

            if (file.LongLength > limits.MaxFileBytes)
            {
                return ImportVerdict.No(
                    $"This file is {Mb(file.LongLength)} and the limit is {Mb(limits.MaxFileBytes)}. "
                    + "The converter can shrink it.");
            }

            if (file.Length < HeaderBytes + ChunkHeaderBytes)
                return ImportVerdict.No("This file is too short to be a model.");

            if (ReadUInt(file, 0) != Magic)
                return ImportVerdict.No("This is not a GLB model. Export it as .glb, not .gltf or .blend.");

            var version = ReadUInt(file, 4);
            if (version != 2)
                return ImportVerdict.No($"This model is glTF version {version}; Islandia reads version 2.");

            var declaredLength = ReadUInt(file, 8);
            if (declaredLength != file.Length)
            {
                // Either truncated in transit or lying about itself. Both end the same way.
                return ImportVerdict.No(
                    $"This file says it is {declaredLength:N0} bytes but is {file.Length:N0}. "
                    + "It is probably incomplete — try copying it again.");
            }

            // ---- chunks -----------------------------------------------------------------------

            byte[] json = null;
            long binaryBytes = 0;
            var binAt = -1;
            var binLength = 0;
            var at = HeaderBytes;
            var chunks = 0;

            while (at + ChunkHeaderBytes <= file.Length)
            {
                var chunkLength = ReadUInt(file, at);
                var chunkType = ReadUInt(file, at + 4);
                var dataAt = at + ChunkHeaderBytes;

                // The check that makes every later number safe: a chunk may not claim more bytes
                // than the file holds. Without it, a declared length of four billion becomes an
                // allocation request.
                if (chunkLength > (uint)(file.Length - dataAt))
                {
                    return ImportVerdict.No(
                        "This file is damaged: part of it claims to be larger than the file itself.");
                }

                if (++chunks > 8)
                    return ImportVerdict.No("This file has more sections than a model should.");

                if (chunkType == JsonChunk)
                {
                    if (json != null) return ImportVerdict.No("This file has two descriptions in it.");
                    if (chunks != 1)
                        return ImportVerdict.No("This file is laid out wrongly: its description is not first.");
                    if (chunkLength > limits.MaxJsonBytes)
                    {
                        return ImportVerdict.No(
                            $"This model's description is {Mb(chunkLength)}, over the "
                            + $"{Mb(limits.MaxJsonBytes)} limit.");
                    }

                    json = new byte[chunkLength];
                    Array.Copy(file, dataAt, json, 0, (int)chunkLength);
                }
                else if (chunkType == BinChunk)
                {
                    binaryBytes += chunkLength;
                    if (binAt < 0)
                    {
                        binAt = dataAt;
                        binLength = (int)chunkLength;
                    }
                }

                // Chunks are padded to four bytes. Advancing by the unpadded length would walk
                // into the middle of the next header.
                var padded = (chunkLength + 3u) & ~3u;
                var next = (long)dataAt + padded;
                if (next <= at) return ImportVerdict.No("This file is damaged: its sections do not advance.");
                at = (int)Math.Min(next, file.Length);
            }

            if (json == null) return ImportVerdict.No("This file has no model description in it.");

            // ---- the description --------------------------------------------------------------

            JObject body;
            try
            {
                body = Json.ParseObject(Encoding.UTF8.GetString(json), "model");
            }
            catch (StreamBoxContractException)
            {
                return ImportVerdict.No("This model's description is not readable.");
            }

            return Measure(body, file, binAt, binLength, binaryBytes, limits);
        }

        private static ImportVerdict Measure(JObject body, byte[] file, int binAt, int binLength,
                                             long binaryBytes, ImportLimits limits)
        {
            var facts = new ImportFacts { FileBytes = file.LongLength };

            var buffers = Objects(body, "buffers");
            var meshes = Objects(body, "meshes");
            var nodes = Objects(body, "nodes");
            var images = Objects(body, "images");
            var accessors = Objects(body, "accessors");

            facts.Meshes = meshes.Count;
            facts.Nodes = nodes.Count;
            facts.Images = images.Count;

            // Counts first, before anything is indexed by them. A file claiming a hundred
            // thousand nodes is refused rather than walked.
            if (buffers.Count > limits.MaxBuffers)
                return ImportVerdict.No($"This model has {buffers.Count} data blocks; Islandia reads "
                                        + $"{limits.MaxBuffers} embedded in the file.", facts);
            if (meshes.Count > limits.MaxMeshes)
                return ImportVerdict.No($"This model has {meshes.Count} meshes and the limit is "
                                        + $"{limits.MaxMeshes}.", facts);
            if (nodes.Count > limits.MaxNodes)
                return ImportVerdict.No($"This model has {nodes.Count} parts and the limit is "
                                        + $"{limits.MaxNodes}.", facts);
            if (images.Count > limits.MaxImages)
                return ImportVerdict.No($"This model has {images.Count} textures and the limit is "
                                        + $"{limits.MaxImages}.", facts);

            // ---- nothing may be fetched from outside ------------------------------------------

            foreach (var buffer in buffers)
            {
                if (Json.OptionalString(buffer, "uri") != null)
                {
                    return ImportVerdict.No(
                        "This model keeps its data in a separate file. Export it as a single "
                        + "self-contained .glb.", facts);
                }
            }

            foreach (var image in images)
            {
                if (Json.OptionalString(image, "uri") != null)
                {
                    return ImportVerdict.No(
                        "This model's textures live outside the file. Export it as a single "
                        + "self-contained .glb with textures embedded.", facts);
                }
            }

            // ---- what it would draw -----------------------------------------------------------

            var perMesh = new List<MeshCost>(meshes.Count);
            foreach (var mesh in meshes)
            {
                var cost = CostOf(mesh, accessors);
                if (cost.Refusal != null) return ImportVerdict.No(cost.Refusal, facts);
                perMesh.Add(cost);
            }

            var walked = WalkScene(nodes, perMesh, limits, facts);
            if (walked != null) return ImportVerdict.No(walked, facts);

            if (facts.Triangles > limits.MaxTriangles)
            {
                return ImportVerdict.No(
                    $"This model has {facts.Triangles:N0} triangles and the limit is "
                    + $"{limits.MaxTriangles:N0}. The converter can simplify it.", facts);
            }

            if (facts.Primitives > limits.MaxPrimitives)
            {
                return ImportVerdict.No(
                    $"This model is drawn in {facts.Primitives} pieces and the limit is "
                    + $"{limits.MaxPrimitives}. Joining its materials would reduce that.", facts);
            }

            // ---- what it would hold -----------------------------------------------------------

            long textureBytes = 0;
            foreach (var image in images)
            {
                var measured = ImageHeader.Measure(image, body, file, binAt, binLength, limits);
                if (measured.Refusal != null) return ImportVerdict.No(measured.Refusal, facts);

                if (measured.Width > facts.MaxTextureDimension) facts.MaxTextureDimension = measured.Width;
                if (measured.Height > facts.MaxTextureDimension) facts.MaxTextureDimension = measured.Height;

                textureBytes += (long)((double)measured.Width * measured.Height * BytesPerPixel * MipFactor);
            }

            if (facts.MaxTextureDimension > limits.MaxTextureDimension)
            {
                return ImportVerdict.No(
                    $"This model has a {facts.MaxTextureDimension}px texture and the limit is "
                    + $"{limits.MaxTextureDimension}px. The converter can resize it.", facts);
            }

            facts.EstimatedBytes = binaryBytes + textureBytes;

            if (facts.EstimatedBytes > limits.MaxEstimatedBytes)
            {
                return ImportVerdict.No(
                    $"This model would need about {Mb(facts.EstimatedBytes)} of memory and the "
                    + $"limit is {Mb(limits.MaxEstimatedBytes)}.", facts);
            }

            return ImportVerdict.Yes(facts);
        }

        // ---- the scene ------------------------------------------------------------------------

        private readonly struct MeshCost
        {
            public MeshCost(int triangles, int primitives, string refusal = null)
            {
                Triangles = triangles;
                Primitives = primitives;
                Refusal = refusal;
            }

            public int Triangles { get; }
            public int Primitives { get; }
            public string Refusal { get; }
        }

        private static MeshCost CostOf(JObject mesh, List<JObject> accessors)
        {
            var primitives = Objects(mesh, "primitives");
            var triangles = 0;

            foreach (var primitive in primitives)
            {
                var mode = Json.OptionalInt(primitive, "mode") ?? Triangles;
                if (mode != Triangles && mode != TriangleStrip && mode != TriangleFan) continue;

                var indices = Json.OptionalInt(primitive, "indices");
                int count;

                if (indices.HasValue)
                {
                    if (indices.Value < 0 || indices.Value >= accessors.Count)
                        return new MeshCost(0, 0, "This model refers to data that is not in it.");
                    count = Json.OptionalInt(accessors[indices.Value], "count") ?? 0;
                }
                else
                {
                    var attributes = Json.OptionalObject(primitive, "attributes");
                    var position = attributes == null ? null : Json.OptionalInt(attributes, "POSITION");
                    if (!position.HasValue) continue;
                    if (position.Value < 0 || position.Value >= accessors.Count)
                        return new MeshCost(0, 0, "This model refers to data that is not in it.");
                    count = Json.OptionalInt(accessors[position.Value], "count") ?? 0;
                }

                if (count < 0) return new MeshCost(0, 0, "This model describes a negative amount of data.");

                var forThis = mode == Triangles ? count / 3 : Math.Max(0, count - 2);
                triangles = Saturate(triangles, forThis);
            }

            return new MeshCost(triangles, primitives.Count);
        }

        /// <summary>
        /// Counts what the scene actually draws, following the node tree.
        /// </summary>
        /// <remarks>
        /// <para>
        /// glTF says nodes form a disjoint union of trees: a node has at most one parent. This
        /// enforces that rather than assuming it, which is what makes the rest safe. A node with
        /// two parents is drawn more than once, and a file is free to arrange that so the number
        /// of draw paths doubles with every level — thirty levels of that is a billion, and
        /// counting them is the denial of service. A node that is its own ancestor is a cycle,
        /// which never terminates at all.
        /// </para>
        /// <para>
        /// With the tree property established, walking from the roots visits each node once, the
        /// counts are exact, and the depth limit means something. An earlier version pushed every
        /// node as a root, which made each one visited at depth zero and the depth check dead
        /// code — a rule that cannot fire, which is worse than no rule because it reads as
        /// handled.
        /// </para>
        /// </remarks>
        private static string WalkScene(List<JObject> nodes, List<MeshCost> perMesh,
                                        ImportLimits limits, ImportFacts facts)
        {
            if (nodes.Count == 0) return null;

            var parents = new int[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                foreach (var child in IntArray(nodes[i], "children"))
                {
                    if (child < 0 || child >= nodes.Count)
                        return "This model refers to a part that is not in it.";
                    if (child == i)
                        return "This model has a part that contains itself.";
                    if (++parents[child] > 1)
                        return "This model shares a part between two parents, which a model may not do.";
                }
            }

            var pending = new Stack<(int Index, int Depth)>();
            for (var i = 0; i < nodes.Count; i++) if (parents[i] == 0) pending.Push((i, 0));

            // Every node having a parent means every node is inside a cycle: no roots, nothing
            // drawable, and a walk that would never have started.
            if (pending.Count == 0) return "This model's parts refer to each other in a loop.";

            var visited = new bool[nodes.Count];

            while (pending.Count > 0)
            {
                var (index, depth) = pending.Pop();

                if (depth > limits.MaxNodeDepth)
                    return $"This model is nested more than {limits.MaxNodeDepth} deep.";

                // The tree property above makes this unreachable. Kept because it is the last
                // thing between a mistake in that reasoning and a walk that does not end.
                if (visited[index]) continue;
                visited[index] = true;

                var node = nodes[index];
                var mesh = Json.OptionalInt(node, "mesh");
                if (mesh.HasValue)
                {
                    if (mesh.Value < 0 || mesh.Value >= perMesh.Count)
                        return "This model refers to a mesh that is not in it.";
                    facts.Triangles = Saturate(facts.Triangles, perMesh[mesh.Value].Triangles);
                    facts.Primitives = Saturate(facts.Primitives, perMesh[mesh.Value].Primitives);
                }

                foreach (var child in IntArray(node, "children")) pending.Push((child, depth + 1));
            }

            return null;
        }

        // ---- reading, carefully ----------------------------------------------------------------

        private static List<JObject> Objects(JObject owner, string field)
        {
            var found = new List<JObject>();
            if (!(owner?[field] is JArray array)) return found;
            foreach (var entry in array) if (entry is JObject item) found.Add(item);
            return found;
        }

        private static List<int> IntArray(JObject owner, string field)
        {
            var found = new List<int>();
            if (!(owner?[field] is JArray array)) return found;
            foreach (var entry in array)
            {
                if (entry.Type == JTokenType.Integer) found.Add(entry.Value<int>());
            }
            return found;
        }

        /// <summary>Adds without wrapping, so an absurd count becomes a large number, not a small one.</summary>
        private static int Saturate(int running, int more)
        {
            if (more <= 0) return running;
            var sum = (long)running + more;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        private static uint ReadUInt(byte[] bytes, int at) =>
            (uint)(bytes[at] | bytes[at + 1] << 8 | bytes[at + 2] << 16 | bytes[at + 3] << 24);

        private static string Mb(long bytes) => $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}
