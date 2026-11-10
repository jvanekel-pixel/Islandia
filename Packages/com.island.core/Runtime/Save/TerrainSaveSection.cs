using System;
using System.IO;
using Island.Core.Terrain;

namespace Island.Core.Save
{
    /// <summary>
    /// Saves and loads the shape of the island.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Heights are stored as 16-bit steps across the sculptable range rather than as floats.
    /// The field is 66,049 samples, so floats would be 258 KB before compression where this is
    /// 129 KB — and the precision given up is about a sixth of a millimetre, against samples a
    /// quarter of a metre apart. Nothing in the world can show the difference.
    /// </para>
    /// <para>
    /// The range the steps are measured against is written into the payload rather than read from
    /// <see cref="IslandConstants"/> at load time. If those limits are ever widened, an island
    /// saved under the old ones must still load at the heights it was built with, not at
    /// heights rescaled by a constant that changed underneath it.
    /// </para>
    /// <para>
    /// Surface weights are already bytes, so they are stored as they are: exactly, with no
    /// conversion to go wrong.
    /// </para>
    /// </remarks>
    public sealed class TerrainSaveSection : ISaveSection
    {
        public const string Id = "TERR";
        public const int Version = 1;

        private readonly Heightfield _field;

        public TerrainSaveSection(Heightfield field) =>
            _field = field ?? throw new ArgumentNullException(nameof(field));

        public string SectionId => Id;
        public int SectionVersion => Version;

        /// <summary>The smallest height difference this format can record.</summary>
        public static float QuantisationStepMetres =>
            (IslandConstants.MaxHeightMetres - IslandConstants.MinHeightMetres) / ushort.MaxValue;

        public byte[] Write()
        {
            var resolution = _field.SampleResolution;
            var min = IslandConstants.MinHeightMetres;
            var max = IslandConstants.MaxHeightMetres;

            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                writer.Write(resolution);
                writer.Write(min);
                writer.Write(max);
                writer.Write(IslandConstants.SurfaceChannelCount);

                for (var z = 0; z < resolution; z++)
                for (var x = 0; x < resolution; x++)
                    writer.Write(Quantise(_field.HeightAt(x, z), min, max));

                // Channel order: sand, grass, rock, path. Read back in the same order.
                for (var z = 0; z < resolution; z++)
                for (var x = 0; x < resolution; x++)
                {
                    writer.Write(_field.WeightAt(x, z, IslandConstants.SurfaceChannelSand));
                    writer.Write(_field.WeightAt(x, z, IslandConstants.SurfaceChannelGrass));
                    writer.Write(_field.WeightAt(x, z, IslandConstants.SurfaceChannelRock));
                    writer.Write(_field.WeightAt(x, z, IslandConstants.SurfaceChannelPath));
                }

                writer.Flush();
                return buffer.ToArray();
            }
        }

        public void Read(byte[] payload, int version)
        {
            if (version != Version)
            {
                throw new SaveFormatException(
                    $"Section '{Id}' is version {version}; this build reads {Version}. " +
                    "Run it through the migrations first.");
            }

            using (var buffer = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(buffer))
            {
                var resolution = reader.ReadInt32();
                var min = reader.ReadSingle();
                var max = reader.ReadSingle();
                var channels = reader.ReadInt32();

                if (resolution != _field.SampleResolution)
                {
                    throw new SaveFormatException(
                        $"This island was saved at {resolution} samples a side; this build uses " +
                        $"{_field.SampleResolution}. Resizing an island is not something this version can do.");
                }
                if (channels != IslandConstants.SurfaceChannelCount)
                {
                    throw new SaveFormatException(
                        $"This island was saved with {channels} surface channels; this build has " +
                        $"{IslandConstants.SurfaceChannelCount}.");
                }
                if (!(max > min))
                    throw new SaveFormatException("This island's height range is not a range.");

                try
                {
                    for (var z = 0; z < resolution; z++)
                    for (var x = 0; x < resolution; x++)
                        _field.SetHeight(x, z, Dequantise(reader.ReadUInt16(), min, max));

                    for (var z = 0; z < resolution; z++)
                    for (var x = 0; x < resolution; x++)
                    {
                        // Written straight back rather than blended: these are the exact weights
                        // the island had, and blending them in one at a time would mix them with
                        // the flat sand a fresh field starts as.
                        var sand = reader.ReadByte();
                        var grass = reader.ReadByte();
                        var rock = reader.ReadByte();
                        var path = reader.ReadByte();
                        _field.SetWeights(x, z, sand, grass, rock, path);
                    }
                }
                catch (EndOfStreamException)
                {
                    throw new SaveFormatException($"Section '{Id}' ends part-way through the island.");
                }
            }
        }

        internal static ushort Quantise(float height, float min, float max)
        {
            var t = (height - min) / (max - min);
            if (t <= 0f) return 0;
            if (t >= 1f) return ushort.MaxValue;
            return (ushort)Math.Round(t * ushort.MaxValue, MidpointRounding.AwayFromZero);
        }

        internal static float Dequantise(ushort stored, float min, float max) =>
            min + (max - min) * (stored / (float)ushort.MaxValue);
    }
}
