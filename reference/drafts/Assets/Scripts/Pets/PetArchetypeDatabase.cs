using System;
using UnityEngine;

namespace IslandBuilder.Pets
{
    public enum PetSpeciesArchetype : byte
    {
        LoyalDog       = 0,
        CozyCat        = 1,
        KitsuneFox     = 2,
        ChillCapybara  = 3,
        RedPanda       = 4,
        MiniDragon     = 5,
        TropicalMacaw  = 6
    }

    public enum CoatPatternStyle : byte
    {
        SolidAndSocks     = 0,
        SpottedOrCalico   = 1,
        TigerOrRingStripe = 2
    }

    [Serializable]
    public struct PetCustomizationProfile
    {
        public PetSpeciesArchetype Archetype;
        public string CustomPetName;
        public Color PrimaryCoatColor;
        public Color SecondaryBellyColor;
        public Color AccentSpotSockColor;
        public CoatPatternStyle PatternStyle;
        [Range(2.0f, 16.0f)] public float PatternFrequency;

        [Header("4 Procedural BlendShape Weights (0..100)")]
        [Range(0f, 100f)] public float EarDroopOrCrestWeight;   // BlendShape 0
        [Range(0f, 100f)] public float SnoutLengthWeight;       // BlendShape 1
        [Range(0f, 100f)] public float TailFluffOrWingWeight;   // BlendShape 2
        [Range(0f, 100f)] public float BodyChubbinessWeight;    // BlendShape 3
    }

    [Serializable]
    public struct PetArchetypeDefinition
    {
        public string DisplaySpeciesName;
        public PetSpeciesArchetype Archetype;
        public GameObject RiggedPrefab;
        public float DefaultWalkSpeedMetersPerSec;
        public float DefaultRunSpeedMetersPerSec;
        public PetCustomizationProfile DefaultAppearance;
    }

    [CreateAssetMenu(fileName = "PetArchetypeDatabase", menuName = "IslandBuilder/Phase 10/Pet Archetype Database")]
    public sealed class PetArchetypeDatabase : ScriptableObject
    {
        public const int TotalArchetypeCount = 7;

        [Header("Shared Compute-Skinned Coat Material")]
        public Material SharedPetCoatMaterial;

        [Header("7 Base Rigged Pet Archetypes (<= 20 Bones, 4 BlendShapes, 3 LODs)")]
        public PetArchetypeDefinition[] Archetypes = new PetArchetypeDefinition[TotalArchetypeCount];

        public static readonly string[] All7SpeciesNames =
        {
            "Loyal Island Dog",
            "Cozy Calico Cat",
            "Island Kitsune Fox",
            "Chill Capybara",
            "Bamboo Red Panda",
            "Pocket Mini-Dragon",
            "Scarlet Tropical Macaw"
        };
    }
}
