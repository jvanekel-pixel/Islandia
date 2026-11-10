using System;

namespace Island.Core.Terrain
{
    /// <summary>
    /// Applies brush strokes to a <see cref="Heightfield"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately a pure function of (field, brush, elapsed time): no frame counters, no stored
    /// stroke state, nothing that reads a clock. That is what lets the whole of sculpting —
    /// the part of this game most likely to feel wrong — be tested in milliseconds without an
    /// engine, a headset, or a frame loop.
    /// </remarks>
    public static class TerrainSculptor
    {
        /// <summary>
        /// A height change smaller than this counts as no change at all: a hundredth of a
        /// millimetre.
        /// </summary>
        /// <remarks>
        /// Flatten and smooth approach their target asymptotically and never quite arrive, so
        /// without a floor a settled brush keeps reporting dirty chunks — and holding the trigger
        /// over finished ground would rebuild four chunk meshes every frame, forever, for a
        /// change nobody can see. Samples are 25 cm apart, so this is four orders of magnitude
        /// below anything the ground could show, and still a hundred times above float noise at
        /// these heights.
        /// </remarks>
        public const float HeightEpsilonMetres = 1e-5f;

        /// <summary>
        /// Applies one step of a stroke and returns what changed.
        /// </summary>
        /// <param name="field">The island. Modified in place.</param>
        /// <param name="brush">What to do, and where.</param>
        /// <param name="elapsedSeconds">
        /// Time since the last step. Strength is a rate, so this is what makes a stroke carve the
        /// same shape at 72 Hz as at 90.
        /// </param>
        public static DirtyRegion Apply(Heightfield field, TerrainBrush brush, float elapsedSeconds)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (elapsedSeconds <= 0f) return DirtyRegion.Empty;

            var (minX, minZ, maxX, maxZ) = SampleWindow(brush);
            if (minX > maxX || minZ > maxZ) return DirtyRegion.Empty;

            // Smooth reads its neighbours while writing them. Without a snapshot the stroke would
            // feed on its own output and drag the ground towards wherever the loop happened to
            // start, which looks like a smear rather than a smoothing.
            var source = brush.Kind == BrushKind.Smooth ? field.CopyHeights() : null;

            // Accumulated sample by sample rather than as a bounding box, so the circle the
            // brush actually drew is what gets rebuilt. The corners of the box are outside the
            // brush, and rebuilding them is pure waste on the stroke people hold down longest.
            var dirty = DirtyRegion.Empty;

            for (var z = minZ; z <= maxZ; z++)
            for (var x = minX; x <= maxX; x++)
            {
                var dx = Heightfield.WorldXOf(x) - brush.WorldX;
                var dz = Heightfield.WorldZOf(z) - brush.WorldZ;
                var distance = (float)Math.Sqrt(dx * dx + dz * dz);
                var falloff = brush.FalloffAt(distance);
                if (falloff <= 0f) continue;

                var changed = brush.Kind == BrushKind.Paint
                    ? Paint(field, brush, x, z, falloff, elapsedSeconds)
                    : Sculpt(field, brush, source, x, z, falloff, elapsedSeconds);

                if (changed) dirty = dirty.WithSample(x, z);
            }

            return dirty;
        }

        /// <summary>
        /// The samples a brush could possibly reach, clamped to the island.
        /// </summary>
        /// <remarks>
        /// Exposed because the dirty-region tests need to know what "exactly the chunks a brush
        /// touches" means, and that answer has to come from one place rather than be restated.
        /// </remarks>
        public static (int MinX, int MinZ, int MaxX, int MaxZ) SampleWindow(TerrainBrush brush)
        {
            var reach = brush.RadiusMetres;
            return (
                Heightfield.GridOf(brush.WorldX - reach),
                Heightfield.GridOf(brush.WorldZ - reach),
                Heightfield.GridOf(brush.WorldX + reach),
                Heightfield.GridOf(brush.WorldZ + reach));
        }

        private static bool Sculpt(Heightfield field, TerrainBrush brush, float[] source,
                                   int x, int z, float falloff, float elapsed)
        {
            var before = field.HeightAt(x, z);
            float after;

            switch (brush.Kind)
            {
                case BrushKind.Raise:
                    after = before + brush.StrengthPerSecond * falloff * elapsed;
                    break;

                case BrushKind.Lower:
                    after = before - brush.StrengthPerSecond * falloff * elapsed;
                    break;

                case BrushKind.Flatten:
                {
                    var t = Approach(brush.StrengthPerSecond * falloff, elapsed);
                    after = before + (brush.FlattenTargetMetres - before) * t;
                    break;
                }

                case BrushKind.Smooth:
                {
                    var average = NeighbourAverage(source, x, z);
                    var t = Approach(brush.StrengthPerSecond * falloff, elapsed);
                    after = before + (average - before) * t;
                    break;
                }

                default:
                    return false;
            }

            after = IslandConstants.ClampHeight(after);
            if (Math.Abs(after - before) < HeightEpsilonMetres) return false;
            field.SetHeight(x, z, after);
            return true;
        }

        private static bool Paint(Heightfield field, TerrainBrush brush, int x, int z,
                                  float falloff, float elapsed)
        {
            var before = field.WeightAt(x, z, brush.SurfaceChannel);
            if (before == 255) return false;
            field.BlendWeight(x, z, brush.SurfaceChannel,
                              Approach(brush.StrengthPerSecond * falloff, elapsed));
            return field.WeightAt(x, z, brush.SurfaceChannel) != before;
        }

        /// <summary>The mean of the samples around one, itself included, clamped at the edges.</summary>
        private static float NeighbourAverage(float[] source, int x, int z)
        {
            var resolution = IslandConstants.HeightfieldResolution;
            var total = 0f;
            var count = 0;
            for (var oz = -1; oz <= 1; oz++)
            for (var ox = -1; ox <= 1; ox++)
            {
                var sx = x + ox;
                var sz = z + oz;
                if (sx < 0 || sx >= resolution || sz < 0 || sz >= resolution) continue;
                total += source[sz * resolution + sx];
                count++;
            }
            return count == 0 ? 0f : total / count;
        }

        /// <summary>
        /// How far to move towards a target in <paramref name="elapsed"/> seconds, for a brush
        /// that approaches rather than adds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is <c>1 - e^(-rate·t)</c> rather than the obvious <c>rate·t</c>, and the
        /// difference is the whole of frame-rate independence for flatten and smooth.
        /// </para>
        /// <para>
        /// Two steps of a plain linear blend leave <c>(1-a)²</c> of the distance, which is not
        /// the <c>(1-2a)</c> one step of twice the length leaves — so the same stroke flattens
        /// harder on a machine drawing more frames. The exponential composes exactly:
        /// <c>e^(-r·t₁)·e^(-r·t₂) = e^(-r·(t₁+t₂))</c>, so only the total time matters. On two
        /// headsets running at 72 and 90 Hz that is the difference between a pad that is level
        /// and one that is nearly level.
        /// </para>
        /// </remarks>
        private static float Approach(float rate, float elapsed)
        {
            if (rate <= 0f || elapsed <= 0f) return 0f;
            var remaining = Math.Exp(-(double)rate * elapsed);
            var moved = 1.0 - remaining;
            return moved <= 0.0 ? 0f : moved >= 1.0 ? 1f : (float)moved;
        }
    }
}
