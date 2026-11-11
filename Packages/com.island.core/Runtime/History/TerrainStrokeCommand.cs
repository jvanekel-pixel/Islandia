using System;
using System.IO;
using System.IO.Compression;
using Island.Core.Terrain;

namespace Island.Core.History
{
    /// <summary>
    /// One brush stroke, as a delta over the rectangle it touched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The draft this replaces had no terrain command at all: spawn, delete, transform and
    /// scatter, and nothing for sculpting. Sculpting is the thing people do most and the thing
    /// they most need to take back — a stroke held a second too long raises a hill that cannot be
    /// put back by hand, because the brush's falloff is not invertible by eye.
    /// </para>
    /// <para>
    /// A stroke is <em>one</em> step, not one per frame. The sculptor runs every frame a button is
    /// held; capture opens when the stroke starts and closes when it ends, so holding the brush
    /// down for three seconds is a single thing to undo.
    /// </para>
    /// <para>
    /// Only the dirty rectangle is stored, and both sides of it: heights and weights as they were,
    /// and as they became. Restoring is then a write rather than a replay, which matters because
    /// replaying a brush backwards does not return the ground to where it started — the
    /// exponential approach in <see cref="TerrainSculptor"/> is not symmetric, and nor is the
    /// weight blend, which renormalises.
    /// </para>
    /// <para>
    /// The payload is deflated. Terrain is smooth, so a region's floats share most of their high
    /// bytes and the compression is substantial — but the test that matters is not the ratio, it
    /// is that a round trip is exact. Quantising, which the save format does to halve its size, is
    /// wrong here: undo that returns the ground to <em>almost</em> where it was leaves a seam.
    /// </para>
    /// </remarks>
    public sealed class TerrainStrokeCommand : IUndoableCommand
    {
        private const int Channels = IslandConstants.SurfaceChannelCount;

        private readonly Heightfield _field;
        private readonly int _minX;
        private readonly int _minZ;
        private readonly int _width;
        private readonly int _depth;

        private byte[] _heightsBefore;
        private byte[] _heightsAfter;
        private byte[] _weightsBefore;
        private byte[] _weightsAfter;
        private bool _discarded;

        private TerrainStrokeCommand(string label, Heightfield field, int minX, int minZ,
                                     int width, int depth,
                                     byte[] heightsBefore, byte[] heightsAfter,
                                     byte[] weightsBefore, byte[] weightsAfter)
        {
            Label = label;
            _field = field;
            _minX = minX;
            _minZ = minZ;
            _width = width;
            _depth = depth;
            _heightsBefore = heightsBefore;
            _heightsAfter = heightsAfter;
            _weightsBefore = weightsBefore;
            _weightsAfter = weightsAfter;
        }

        public string Label { get; }

        /// <summary>The four compressed blobs, plus a little for the object itself.</summary>
        public long Bytes { get; private set; }

        /// <summary>Empty: the terrain is not an instance and has no id.</summary>
        public long[] Subjects => Array.Empty<long>();

        /// <summary>How many samples the stroke touched.</summary>
        public int SampleCount => _width * _depth;

        /// <summary>
        /// Begins recording a stroke. Call before the first brush application, then
        /// <see cref="Capture.Complete"/> when the stroke ends.
        /// </summary>
        public static Capture Begin(Heightfield field, string label)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            return new Capture(field, label ?? "Sculpt");
        }

        public void Undo() => Write(_heightsBefore, _weightsBefore);

        public void Redo() => Write(_heightsAfter, _weightsAfter);

        public void Discard()
        {
            // The blobs are the only thing of size here. Releasing them lets a long session's
            // evicted history be collected instead of held by a list nobody reads.
            _heightsBefore = null;
            _heightsAfter = null;
            _weightsBefore = null;
            _weightsAfter = null;
            _discarded = true;
        }

        private void Write(byte[] heights, byte[] weights)
        {
            if (_discarded)
                throw new InvalidOperationException(
                    $"'{Label}' has left history and cannot be applied. Something is holding a " +
                    "command the stack already released.");

            var samples = _width * _depth;
            var heightValues = Floats(heights, samples);
            var weightValues = Inflate(weights, samples * Channels);

            var at = 0;
            for (var z = 0; z < _depth; z++)
            {
                for (var x = 0; x < _width; x++, at++)
                {
                    _field.SetHeight(_minX + x, _minZ + z, heightValues[at]);
                    var w = at * Channels;
                    _field.SetWeights(_minX + x, _minZ + z,
                        weightValues[w + IslandConstants.SurfaceChannelSand],
                        weightValues[w + IslandConstants.SurfaceChannelGrass],
                        weightValues[w + IslandConstants.SurfaceChannelRock],
                        weightValues[w + IslandConstants.SurfaceChannelPath]);
                }
            }
        }

        /// <summary>
        /// An open stroke: the "before" of a rectangle, waiting for the stroke to finish.
        /// </summary>
        public sealed class Capture
        {
            private readonly Heightfield _field;
            private readonly string _label;
            private readonly float[] _heights;
            private readonly byte[] _weights;

            internal Capture(Heightfield field, string label)
            {
                _field = field;
                _label = label;
                // The whole field, because which rectangle the stroke will touch is not known
                // until it ends. One megabyte held for the length of a stroke, released when it
                // closes; the alternative is guessing the region from the first frame's brush and
                // being wrong the moment the person moves their hand.
                _heights = field.CopyHeights();
                _weights = CopyWeights(field);
            }

            /// <summary>
            /// Closes the stroke over the region it changed, returning the step to record — or
            /// null when nothing actually moved, which is the common case for a tap that misses.
            /// </summary>
            public TerrainStrokeCommand Complete(DirtyRegion region)
            {
                if (region.IsEmpty) return null;

                var minX = region.MinX;
                var minZ = region.MinZ;
                var width = region.MaxX - region.MinX + 1;
                var depth = region.MaxZ - region.MinZ + 1;

                var samples = width * depth;
                var heightsBefore = new float[samples];
                var heightsAfter = new float[samples];
                var weightsBefore = new byte[samples * Channels];
                var weightsAfter = new byte[samples * Channels];

                var at = 0;
                for (var z = 0; z < depth; z++)
                {
                    for (var x = 0; x < width; x++, at++)
                    {
                        var gridX = minX + x;
                        var gridZ = minZ + z;
                        var index = Heightfield.IndexOf(gridX, gridZ);

                        heightsBefore[at] = _heights[index];
                        heightsAfter[at] = _field.HeightAt(gridX, gridZ);

                        for (var c = 0; c < Channels; c++)
                        {
                            weightsBefore[at * Channels + c] = _weights[index * Channels + c];
                            weightsAfter[at * Channels + c] = _field.WeightAt(gridX, gridZ, c);
                        }
                    }
                }

                var command = new TerrainStrokeCommand(
                    _label, _field, minX, minZ, width, depth,
                    Deflate(ToBytes(heightsBefore)), Deflate(ToBytes(heightsAfter)),
                    Deflate(weightsBefore), Deflate(weightsAfter));

                command.Bytes = 64
                    + command._heightsBefore.Length + command._heightsAfter.Length
                    + command._weightsBefore.Length + command._weightsAfter.Length;
                return command;
            }

            /// <summary>What the stroke would cost to store, before it is closed.</summary>
            public int HeldSamples => _heights.Length;

            private static byte[] CopyWeights(Heightfield field)
            {
                var resolution = field.SampleResolution;
                var copy = new byte[resolution * resolution * Channels];
                for (var z = 0; z < resolution; z++)
                {
                    for (var x = 0; x < resolution; x++)
                    {
                        var at = Heightfield.IndexOf(x, z) * Channels;
                        for (var c = 0; c < Channels; c++) copy[at + c] = field.WeightAt(x, z, c);
                    }
                }
                return copy;
            }
        }

        // ---- the bytes ------------------------------------------------------------------------

        private static byte[] ToBytes(float[] values)
        {
            var raw = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, raw, 0, raw.Length);
            return raw;
        }

        private static float[] Floats(byte[] compressed, int count)
        {
            var raw = Inflate(compressed, count * sizeof(float));
            var values = new float[count];
            Buffer.BlockCopy(raw, 0, values, 0, raw.Length);
            return values;
        }

        private static byte[] Deflate(byte[] raw)
        {
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, true))
                {
                    deflate.Write(raw, 0, raw.Length);
                }
                return output.ToArray();
            }
        }

        private static byte[] Inflate(byte[] compressed, int expected)
        {
            var raw = new byte[expected];
            using (var input = new MemoryStream(compressed, false))
            using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                var at = 0;
                while (at < expected)
                {
                    var read = inflate.Read(raw, at, expected - at);
                    if (read <= 0)
                        throw new InvalidDataException(
                            $"A terrain delta unpacked to {at} bytes, not the {expected} it should hold.");
                    at += read;
                }
            }
            return raw;
        }
    }
}
