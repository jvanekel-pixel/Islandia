namespace Island.Core
{
    /// <summary>
    /// Every number that describes the shape of the island, in one place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The architecture baseline (<c>docs/plan/02-ARCHITECTURE.md</c>, ADR-08) says these are
    /// defined once and nothing else may hard-code them. That rule exists because the original
    /// blueprint did the opposite: the water line appeared as <c>-0.35</c>, <c>-0.30</c> and
    /// <c>-0.25</c> in different files, the sand line disagreed with its own comment, and the
    /// cliff angle was 38 degrees in the prose and 35 in the code. Those are not typos you find
    /// by reading — they are a shoreline that does not line up with the foam drawn on it.
    /// </para>
    /// <para>
    /// Values that follow from others are computed here rather than written down, so they cannot
    /// drift apart. The tests assert the derived ones against the figures the plan quotes.
    /// </para>
    /// </remarks>
    public static class IslandConstants
    {
        // ---- The landmass -------------------------------------------------------------------

        /// <summary>Width and depth of the buildable island, in metres. Centred on the origin.</summary>
        public const float IslandExtentMetres = 64f;

        /// <summary>Half the extent: world coordinates run from -32 to +32 on both X and Z.</summary>
        public const float IslandHalfExtentMetres = IslandExtentMetres / 2f;

        /// <summary>Roughly one acre (4,047 m²); this is 4,096 m², about 1.012 acres.</summary>
        public const float IslandAreaSquareMetres = IslandExtentMetres * IslandExtentMetres;

        // ---- Chunking -----------------------------------------------------------------------

        /// <summary>Chunks along one edge. The island is this squared.</summary>
        public const int ChunksPerSide = 8;

        /// <summary>Total chunks: 64.</summary>
        public const int ChunkCount = ChunksPerSide * ChunksPerSide;

        /// <summary>Width and depth of one chunk, in metres: 8.</summary>
        public const float ChunkSizeMetres = IslandExtentMetres / ChunksPerSide;

        /// <summary>Distance between neighbouring height samples, in metres.</summary>
        public const float VertexSpacingMetres = 0.25f;

        /// <summary>Vertices along one edge of a chunk, counting both borders: 33.</summary>
        public const int ChunkVerticesPerSide = (int)(ChunkSizeMetres / VertexSpacingMetres) + 1;

        /// <summary>Vertices in one chunk: 1,089.</summary>
        public const int ChunkVertexCount = ChunkVerticesPerSide * ChunkVerticesPerSide;

        /// <summary>Quads along one edge of a chunk: 32.</summary>
        public const int ChunkQuadsPerSide = ChunkVerticesPerSide - 1;

        /// <summary>Triangles in one chunk at full resolution: 2,048.</summary>
        public const int ChunkTriangleCount = ChunkQuadsPerSide * ChunkQuadsPerSide * 2;

        /// <summary>Triangles across the whole island at full resolution: 131,072.</summary>
        public const int IslandTriangleCount = ChunkTriangleCount * ChunkCount;

        // ---- The shared heightfield ---------------------------------------------------------

        /// <summary>
        /// Height samples along one edge of the island: 257. Every chunk reads from this one
        /// field, so two chunks sharing an edge read the identical sample and a seam cannot
        /// crack — which is also why normals must be solved across it rather than per chunk.
        /// </summary>
        public const int HeightfieldResolution = ChunksPerSide * ChunkQuadsPerSide + 1;

        /// <summary>Total height samples: 66,049.</summary>
        public const int HeightfieldSampleCount = HeightfieldResolution * HeightfieldResolution;

        /// <summary>Lowest the ground may be sculpted, in metres. Deep enough for a pond.</summary>
        public const float MinHeightMetres = -3.5f;

        /// <summary>Highest the ground may be sculpted, in metres.</summary>
        public const float MaxHeightMetres = 18f;

        // ---- Water --------------------------------------------------------------------------

        /// <summary>
        /// The water plane, in metres. Flat ground sits at 0, so ground sculpted below this
        /// floods: that is how ponds are dug. Nothing else may hard-code this value.
        /// </summary>
        public const float WaterLevelMetres = -0.35f;

        /// <summary>
        /// Ground below this is wet sand rather than dry: a little above the water line, so the
        /// shore reads as shore. Defined relative to the water, never written down separately.
        /// </summary>
        public const float SandLineMetres = WaterLevelMetres + 0.15f;

        /// <summary>
        /// Land plants are hidden below this. Slightly under the water line, so a plant at the
        /// very edge does not flicker as the shore is nudged.
        /// </summary>
        public const float PlantSubmergedCutoffMetres = WaterLevelMetres - 0.05f;

        /// <summary>A new island is sand below this and starts turning to grass above it: +0.1 m.</summary>
        public const float GrassLineMetres = SandLineMetres + 0.3f;

        /// <summary>A new island is all grass above this: about +0.5 m.</summary>
        public const float FullGrassMetres = GrassLineMetres + 0.4f;

        // ---- The station and its track (ADR-24) ---------------------------------------------

        /// <summary>Length of the station's reserved footprint along the track, in metres.</summary>
        public const float StationFootprintLengthMetres = 12f;

        /// <summary>Width of the station's reserved footprint across the track, in metres.</summary>
        public const float StationFootprintWidthMetres = 8f;

        /// <summary>Width of the reserved corridor the track leaves the island along, in metres.</summary>
        public const float TrackCorridorWidthMetres = 4f;

        /// <summary>
        /// The shortest the corridor may be: open water at least this far in front of the
        /// station. Ground that reaches nearer the edge is cut back to water under the track.
        /// </summary>
        public const float TrackCorridorMinLengthMetres = 4f;

        /// <summary>The height the station's footprint is levelled to, in metres: a dry platform.</summary>
        public const float StationPlatformHeightMetres = 0.5f;

        /// <summary>The depth the corridor is kept at or below, in metres, so the track always crosses water.</summary>
        public const float TrackCorridorFloorMetres = WaterLevelMetres - 0.4f;

        // ---- Surface weights ------------------------------------------------------------------

        /// <summary>Vertex colour channel carrying sand, the surface a fresh island starts as.</summary>
        public const int SurfaceChannelSand = 0;

        /// <summary>Vertex colour channel carrying grass, or astroturf when turf mode is on.</summary>
        public const int SurfaceChannelGrass = 1;

        /// <summary>Vertex colour channel carrying rock. Applied automatically on steep ground.</summary>
        public const int SurfaceChannelRock = 2;

        /// <summary>Vertex colour channel carrying path.</summary>
        public const int SurfaceChannelPath = 3;

        /// <summary>How many surface weights a vertex carries: one per colour channel.</summary>
        public const int SurfaceChannelCount = 4;

        /// <summary>
        /// Ground steeper than this gets rock blended in automatically, in degrees. The blueprint
        /// quoted 38 in its prose and used 35 in its code; 35 is the behaviour people saw.
        /// </summary>
        public const float AutoRockSlopeDegrees = 35f;

        // ---- Scale --------------------------------------------------------------------------

        /// <summary>
        /// How much smaller the world looks in tabletop mode. The whole island sits in front of
        /// the player as a model at 1:25.
        /// </summary>
        public const float TabletopScaleDivisor = 25f;

        // ---- Helpers ------------------------------------------------------------------------

        /// <summary>
        /// Whether a height is at or below the water line, and so under water.
        /// </summary>
        public static bool IsSubmerged(float heightMetres) => heightMetres <= WaterLevelMetres;

        /// <summary>
        /// Clamps a height to the sculptable range.
        /// </summary>
        public static float ClampHeight(float heightMetres) =>
            heightMetres < MinHeightMetres ? MinHeightMetres
            : heightMetres > MaxHeightMetres ? MaxHeightMetres
            : heightMetres;

        /// <summary>
        /// Whether a world X or Z coordinate lies on the island rather than out at sea.
        /// </summary>
        public static bool IsInsideIsland(float x, float z) =>
            x >= -IslandHalfExtentMetres && x <= IslandHalfExtentMetres &&
            z >= -IslandHalfExtentMetres && z <= IslandHalfExtentMetres;
    }
}
