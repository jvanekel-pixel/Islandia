using System;
using UnityEngine;

namespace IslandBuilder.Architecture
{
    public enum ArchitecturalCategory : byte
    {
        GazeboOrPavilion = 0, // 28 Prefabs (2,500 -> 400 tris)
        ModularBuilding  = 1, // 95 Modules across 5 Themes (200 -> 40 tris)
        RockOrBoulder    = 2, // 20 Items (150 -> 40 tris)
        OutdoorLivingProp = 3,// 75 Items (150 -> 40 tris, Baked Vertex Glow on lanterns/torches)
        LiveMediaOrPetProp = 4// 15 Items (Spotify Radio, TV, Cinema Screen, Projector, Pet Toys)
    }

    public enum ArchitecturalTheme : byte
    {
        UniversalPropOrGazebo = 0,
        TropicalVilla = 1,
        ModernConcreteWood = 2,
        CozyTimberCottage = 3,
        JapaneseRyokan = 4,
        MediterraneanStucco = 5
    }

    public enum StructuralModuleSubType : byte
    {
        None = 0,
        FoundationBlock = 1,
        StiltedWaterFoundation = 2,
        SolidWall = 3,
        HalfWall = 4,
        CornerPillar = 5,
        WindowWallOpaqueMatCap = 6,
        ArchedDoorway = 7,
        DoubleDoor = 8,
        GabledRoofSlope = 9,
        FlatRoofTerrace = 10,
        RoofOverhang = 11,
        StraightStaircase = 12,
        SpiralOrLStaircase = 13,
        CantileveredBalcony = 14,
        WoodenDeckTile = 15,
        PierDockSection = 16,
        RopeBridgeSegment = 17,
        StoneArchBridgeSegment = 18,
        ZenBridgeOrLighthouse = 19
    }

    [Serializable]
    public struct ArchitecturalCatalogItem
    {
        public string DisplayName;
        public int CatalogId; // 0..232
        public ArchitecturalCategory Category;
        public ArchitecturalTheme Theme;
        public StructuralModuleSubType ModuleSubType;

        [Header("Prefab & 3-Tier LOD Meshes")]
        public GameObject Prefab;
        public Mesh Lod0Mesh;
        public Mesh Lod1Mesh;
        public Mesh Lod2Mesh;

        [Header("Placement & Welding Rules")]
        public float FoundationRadiusMeters;
        public bool AutoLevelTerrainOnPlace;
        public bool CanWeldIntoStaticAssembly;
        public bool UsesOpaqueMatCapGlass;
        public bool HasBakedVertexEmissiveGlow;
    }

    [CreateAssetMenu(fileName = "ArchitecturalCatalogDatabase", menuName = "IslandBuilder/Phase 5/Architectural Catalog Database")]
    public sealed class ArchitecturalCatalogDatabase : ScriptableObject
    {
        public const int GazeboCount = 28;
        public const int ModularBuildingCount = 95; // 5 Themes x 19 Structural Modules
        public const int RockAndPropCount = 110;    // 20 Rocks + 75 Outdoor Living + 15 Live Media/Pet
        public const int TotalArchitecturalItems = GazeboCount + ModularBuildingCount + RockAndPropCount; // 233

        [Header("Unified Single-Material Atlas")]
        public Material SharedArchitecturalMaterial;
        public Texture2D SharedArchitecturalAtlas;
        public Texture2D SharedGlassMatCapTexture;

        [Header("233 Pre-Loaded Items (28 Gazebos + 95 Building Modules + 110 Props)")]
        public ArchitecturalCatalogItem[] Items = new ArchitecturalCatalogItem[TotalArchitecturalItems];

        public static readonly string[] All28GazeboNames =
        {
            "Victorian Wrought-Iron Gazebo", "Cedar Octagonal Gazebo", "Japanese Tea Pavilion",
            "Balinese Thatch Bale", "Modern Louvered Pergola", "Classical Greek Rotunda",
            "Moroccan Courtyard Pavilion", "Rustic Timber Cabana", "Royal Glass Conservatory (Opaque MatCap)",
            "Zen Moon-Viewing Pavilion", "Beachfront Tiki Bar Pavilion", "Stone Arch Belvedere",
            "Geodesic Garden Dome (Opaque MatCap)", "Vineyard Trellis Arbor", "Colonial Bandstand",
            "Floating Lotus Pavilion", "Bamboo Meditation Hut", "Copper-Roofed Rotunda",
            "Sun-Sail Modern Deck", "Wisteria Pergola Tunnel", "Alpine Chalet Gazebo",
            "Cliffside Overlook Deck", "Starlight Observatory Dome", "Driftwood Beach Shelter",
            "Victorian Orangerie Conservatory", "Sunken Fire-Pit Pavilion", "Hanging Lantern Pagoda",
            "Twin-Bridge Water Pavilion"
        };

        public static readonly string[] All5ThemeNames =
        {
            "Tropical Villa", "Modern Concrete & Wood", "Cozy Timber Cottage",
            "Japanese Ryokan", "Mediterranean Stucco"
        };

        public static readonly string[] All19ModuleTypeNames =
        {
            "Foundation Block (2x2m)", "Stilted Water Foundation", "Solid Wall (2x3m)",
            "Half Courtyard Wall", "Structural Corner Pillar", "Picture Window Wall (MatCap Glass)",
            "Arched Entry Doorway", "Double French Door", "Gabled Roof Slope",
            "Flat Roof Terrace", "Thatch/Tile Roof Overhang", "Straight Timber Staircase",
            "L-Turn Landing Staircase", "Cantilevered Balcony", "Boardwalk Deck Tile",
            "Waterfront Pier Section", "Rope Suspension Bridge Segment", "Stone Arch Bridge Segment",
            "Zen Curved Bridge & Beacon"
        };

        public static readonly string[] All20RockNames =
        {
            "Granite Outcrop Large", "Granite Outcrop Medium", "Basalt Hex Column Cluster Tall",
            "Basalt Hex Stepping Cluster", "Sandstone Desert Archway", "Sandstone Weathered Pillar",
            "Mossy River Stone Trio", "Mossy Creek Boulder", "Flat Zen Stepping Stone Path",
            "Circular Garden Stepping Stones", "Limestone Karst Spire", "Limestone Cavern Ledge",
            "Volcanic Obsidian Crag", "Volcanic Shore Rock", "Zen Raked Gravel Monolith",
            "Coastal Wave-Cut Boulder", "Tidepool Hollow Rock", "Waterfall Cliff Ledge Large",
            "Waterfall Splash Rock", "Sculpted Taihu Scholar's Rock"
        };

        public static readonly string[] All15LiveMediaAndPetPropNames =
        {
            "Spotify Box Radio (Spatial Audio)", "Retro 80s Boombox", "Drive-In Outdoor Cinema Screen",
            "Wall-Mounted Flatscreen TV", "Portable Tripod Home Projector", "Cozy Timber Pet House",
            "Pet Agility Jump Hoop", "Pet Weave Slalom Poles", "Pet Balance Seesaw",
            "Ceramic Pet Food & Water Bowls", "Interactive Fetch Tennis Ball", "Aerodynamic Flying Disc Toy",
            "Plush Orthopedic Pet Bed", "Sisal Cat Scratching Post", "Harmonic Island Pet Bell"
        };
    }
}
