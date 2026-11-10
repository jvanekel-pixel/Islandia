namespace Island.Core.Terrain
{
    /// <summary>What a stroke does to the ground.</summary>
    public enum BrushKind
    {
        /// <summary>Push the ground up.</summary>
        Raise,
        /// <summary>Push it down. Below the water line this digs a pond.</summary>
        Lower,
        /// <summary>Pull it towards one height, for a building pad or a path.</summary>
        Flatten,
        /// <summary>Average with the neighbours, to take the edge off.</summary>
        Smooth,
        /// <summary>Change the surface — sand, grass, rock, path — without moving the ground.</summary>
        Paint,
    }

    /// <summary>One application of a brush, in world units.</summary>
    /// <remarks>
    /// <para>
    /// <see cref="Strength"/> is a <em>rate</em>, per second, not an amount. The caller multiplies
    /// by the frame's elapsed time, so a stroke carves the same shape at 72 Hz as at 90 — which
    /// matters here more than usual, because the two headsets this has to run on differ in exactly
    /// that. The blueprint's version integrated per frame and so dug deeper on a faster machine.
    /// </para>
    /// <para>
    /// Falloff is smooth at both ends: full effect at the centre, nothing at the rim, and no
    /// sudden step at either. A linear falloff leaves a visible crease around the edge of every
    /// stroke.
    /// </para>
    /// </remarks>
    public readonly struct TerrainBrush
    {
        /// <summary>The largest brush the tools offer, in metres.</summary>
        public const float MaxRadiusMetres = 8f;

        /// <summary>The smallest brush that still covers a sample or two.</summary>
        public const float MinRadiusMetres = 0.25f;

        public TerrainBrush(BrushKind kind, float worldX, float worldZ, float radiusMetres,
                            float strengthPerSecond, int surfaceChannel = IslandConstants.SurfaceChannelGrass,
                            float flattenTargetMetres = 0f)
        {
            Kind = kind;
            WorldX = worldX;
            WorldZ = worldZ;
            RadiusMetres =
                radiusMetres < MinRadiusMetres ? MinRadiusMetres
                : radiusMetres > MaxRadiusMetres ? MaxRadiusMetres
                : radiusMetres;
            StrengthPerSecond = strengthPerSecond;
            SurfaceChannel = surfaceChannel;
            FlattenTargetMetres = flattenTargetMetres;
        }

        public BrushKind Kind { get; }
        public float WorldX { get; }
        public float WorldZ { get; }

        /// <summary>Clamped into the range the tools allow.</summary>
        public float RadiusMetres { get; }

        /// <summary>Metres per second for the height brushes; a rate of change for the others.</summary>
        public float StrengthPerSecond { get; }

        /// <summary>Which surface <see cref="BrushKind.Paint"/> lays down.</summary>
        public int SurfaceChannel { get; }

        /// <summary>
        /// The height <see cref="BrushKind.Flatten"/> pulls towards. Captured once when the stroke
        /// starts, not re-read each frame: re-reading would let the pad drift under its own brush.
        /// </summary>
        public float FlattenTargetMetres { get; }

        /// <summary>
        /// How strongly the brush acts at a given distance from its centre: 1 at the middle,
        /// 0 at the rim, smooth at both.
        /// </summary>
        public float FalloffAt(float distanceMetres)
        {
            if (distanceMetres >= RadiusMetres) return 0f;
            if (distanceMetres <= 0f) return 1f;
            var t = 1f - distanceMetres / RadiusMetres;
            return t * t * (3f - 2f * t);   // smoothstep
        }
    }
}
