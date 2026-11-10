using System;
using UnityEngine;

namespace IslandBuilder.Foliage
{
    public enum BotanicalCategory : byte
    {
        Tree = 0,          // 36 Species (1,200 -> 180 tris + Billboard)
        BushOrShrub = 1,   // 42 Species (450 -> 60 tris)
        FlowerCluster = 2, // 32 Base Meshes x 10 LUT Cultivars = 320 Varieties (80 -> 16 tris)
        NaturalGrass = 3   // GPU-Instanced Opaque Grass Clump (36 -> 12 tris)
    }

    [Serializable]
    public struct BotanicalMeshEntry
    {
        public string SpeciesName;
        public int MeshArchetypeId; // 0..110
        public BotanicalCategory Category;

        [Header("4-Tier Opaque Silhouette LOD Chain")]
        public Mesh Lod0Mesh;
        public Mesh Lod1Mesh;
        public Mesh Lod2Mesh;
        public Mesh Lod3BillboardMesh;

        [Header("LOD Transition Distances (Meters at 1:1 Scale)")]
        public float Lod0To1Distance;
        public float Lod1To2Distance;
        public float Lod2To3Distance;
        public float MaxCullDistance;

        [Header("Placement Rules")]
        public float BoundingSphereRadius;
        public float DefaultMinSpacingMeters;
        public Vector3 BaseTint;
    }

    [Serializable]
    public struct FlowerCultivarPalette
    {
        public string CultivarName;
        [Range(0.15f, 0.98f)] public float LutRowCoordinateV;
        public Color PetalBaseColor;
        public Color PetalTipColor;
    }

    [CreateAssetMenu(fileName = "BotanicalCatalogDatabase", menuName = "IslandBuilder/Phase 4/Botanical Catalog Database")]
    public sealed class BotanicalCatalogDatabase : ScriptableObject
    {
        public const int TreeSpeciesCount = 36;
        public const int BushSpeciesCount = 42;
        public const int BaseFlowerMeshCount = 32;
        public const int CultivarsPerFlower = 10;
        public const int TotalFlowerVarieties = BaseFlowerMeshCount * CultivarsPerFlower; // 320
        public const int GrassArchetypeIndex = 110;
        public const int TotalMeshArchetypes = TreeSpeciesCount + BushSpeciesCount + BaseFlowerMeshCount + 1; // 111

        [Header("Shared Unified Material & LUT")]
        public Material SharedBotanicalMaterial;
        public Texture2D BotanicalPaletteLutTexture;

        [Header("111 Base Botanical Mesh Archetypes (36 Trees + 42 Bushes + 32 Flowers + 1 Grass)")]
        public BotanicalMeshEntry[] MeshArchetypes = new BotanicalMeshEntry[TotalMeshArchetypes];

        [Header("10 LUT Cultivar Palettes (32 Flowers x 10 = 320 Flower Varieties)")]
        public FlowerCultivarPalette[] FlowerCultivars = new FlowerCultivarPalette[CultivarsPerFlower];

        /// <summary>
        /// Resolves any of the 398 pre-loaded botanical catalog IDs (0..397) into its Base Mesh Archetype (0..109)
        /// and its LUT Cultivar V coordinate.
        /// Catalog ID Layout:
        ///   0 .. 35   : 36 Tree Species
        ///   36 .. 77  : 42 Bush Species
        ///   78 .. 397 : 320 Flower Varieties (32 Base Flower Meshes x 10 Cultivars)
        ///   398       : Natural Grass Blade Cluster
        /// </summary>
        public void ResolveCatalogVariety(int catalogVarietyId, out int meshArchetypeId, out float cultivarLutV, out Vector3 baseTint)
        {
            if (catalogVarietyId < 78)
            {
                // Trees (0..35) or Bushes (36..77)
                meshArchetypeId = Mathf.Clamp(catalogVarietyId, 0, 77);
                cultivarLutV = (meshArchetypeId % 2 == 0) ? 0.15f : 0.25f;
                baseTint = MeshArchetypes[meshArchetypeId].BaseTint;
                return;
            }

            if (catalogVarietyId >= 398)
            {
                // Natural Grass Blade Cluster
                meshArchetypeId = GrassArchetypeIndex;
                cultivarLutV = 0.05f;
                baseTint = MeshArchetypes[GrassArchetypeIndex].BaseTint;
                return;
            }

            // 320 Flowers (Catalog IDs 78..397)
            int flowerIndex = catalogVarietyId - 78;
            int baseFlowerOffset = flowerIndex / CultivarsPerFlower; // 0..31
            int cultivarIdx = flowerIndex % CultivarsPerFlower;      // 0..9

            meshArchetypeId = 78 + Mathf.Clamp(baseFlowerOffset, 0, BaseFlowerMeshCount - 1);
            cultivarLutV = FlowerCultivars != null && FlowerCultivars.Length > cultivarIdx
                ? FlowerCultivars[cultivarIdx].LutRowCoordinateV
                : 0.15f + cultivarIdx * 0.085f;
            baseTint = Vector3.one;
        }

        public static readonly string[] All36TreeNames =
        {
            // Tropical (9)
            "Coconut Palm", "Royal Palm", "Banana Tree", "Banyan Tree", "Red Mangrove",
            "Plumeria Tree", "Pandanus Screw Pine", "Traveler's Palm", "Socotra Dragon Blood",
            // Temperate & Deciduous (12)
            "Ancient English Oak", "Weeping Willow", "Silver Birch", "Japanese Red Maple", "Golden Ginkgo Biloba",
            "Sakura Cherry Blossom", "Copper Beech", "Violet Jacaranda", "Southern Magnolia", "Lombardy Poplar",
            "American Elm", "Sweet Chestnut",
            // Alpine & Conifer (8)
            "Colorado Blue Spruce", "Douglas Fir", "Scots Pine", "Italian Cypress", "Giant Sequoia",
            "Cedar of Lebanon", "Golden Larch", "Twisted Mountain Juniper",
            // Arid & Sculptural (7)
            "African Baobab", "Mojave Joshua Tree", "Gnarled Olive Tree", "Umbrella Thorn Acacia", "Saguaro Cactus",
            "Rainbow Eucalyptus", "Giant Sculptural Bonsai"
        };

        public static readonly string[] All42BushNames =
        {
            // Flowering Shrubs (14)
            "Kurume Azalea", "Alpine Rhododendron", "Trailing Bougainvillea", "Japonica Camellia", "Tropical Hibiscus Bush",
            "Mediterranean Oleander", "Golden Forsythia", "French Lilac", "Tree Peony Shrub", "Fragrant Gardenia",
            "Hanging Fuchsia", "Sunset Lantana", "Bridalwreath Spiraea", "Cascading Wisteria Shrub",
            // Foliage & Topiary (16)
            "Boxwood Hedge Straight", "Boxwood Hedge Corner", "Boxwood Hedge Arch", "Boxwood Topiary Sphere",
            "Boxwood Topiary Spiral", "Boxwood Topiary Pyramid", "English Privet", "Variegated Holly",
            "Creeping Juniper Bush", "Heavenly Bamboo Nandina", "Red Robin Photinia", "Golden Euonymus",
            "Tobira Pittosporum", "Crimson Pygmy Barberry", "Dwarf Myrtle Hedge", "Sculpted Cloud Boxwood",
            // Ferns & Grasses (12)
            "Boston Sword Fern", "Mounted Staghorn Fern", "Swiss Cheese Monstera", "Giant Alocasia Elephant Ear",
            "Silver Pampas Grass Plume", "Emerald Bamboo Clump", "Golden Bamboo Clump", "Black Timber Bamboo Clump",
            "Blue Weber Agave", "Medicinal Aloe Vera Clump", "Beaked Yucca Rosette", "Variegated Hosta Mound"
        };

        public static readonly string[] All32BaseFlowerNames =
        {
            "Hybrid Tea Rose Bush", "Climbing Rose Trellis", "Single Stem Rose", "Dutch Tulip Cluster",
            "Giant Sunflowers", "Phalaenopsis Orchid Spray", "Bigleaf Hydrangea Mound", "English Lavender Hedgelet",
            "Crane Bird of Paradise", "Sacred Pond Lotus", "Floating Water Lily Pad", "California Golden Poppy",
            "Shasta Daisy Patch", "Dinnerplate Dahlia", "French Marigold Cluster", "Woodland Foxglove Spire",
            "Russell Lupine Spike", "Bearded Iris Clump", "Trumpet Daffodil Cluster", "Grape Hyacinth Border",
            "Autumn Chrysanthemum", "Fringed Carnation", "Giant Dahlia-Flowered Zinnia", "Tall Garden Snapdragon",
            "Wax Begonia Bed", "Cascading Wave Petunia", "Velvet Garden Pansy", "Hawaiian Hibiscus Blossom",
            "Frangipani Plumeria Cluster", "South African King Protea", "Trumpet Calla Lily", "Wildflower Clover Meadow"
        };
    }
}
