using System;
using System.Collections.Generic;
using IslandBuilder.Core;
using IslandBuilder.TerrainSystem;
using UnityEngine;

namespace IslandBuilder.Pets
{
    public sealed class IslandPetSanctuaryManager : MonoBehaviour
    {
        public static IslandPetSanctuaryManager Instance { get; private set; }

        [Header("Pet Archetype Catalog")]
        [SerializeField] private PetArchetypeDatabase archetypeDatabase;

        [Header("Sanctuary Customization Turntable Preview")]
        [SerializeField] private IslandPetController studioPreviewMannequin;
        [SerializeField] private Vector3 defaultPetSpawnPos = new Vector3(-2.0f, 0.0f, 2.5f);

        [Header("Starter Companion Seeding")]
        [SerializeField] private bool spawnStarterCompanionsOnStart = true;

        private readonly List<IslandPetController> _activeSpawnedPets = new List<IslandPetController>(8);
        private readonly List<SavedPetRecord> _restingSanctuaryPets = new List<SavedPetRecord>(32);

        private PetCustomizationProfile _workingStudioProfile;
        private Transform _petsContainerRoot;

        public IReadOnlyList<IslandPetController> ActiveSpawnedPets => _activeSpawnedPets;
        public PetArchetypeDatabase ArchetypeDatabase => archetypeDatabase;
        public PetCustomizationProfile WorkingStudioProfile => _workingStudioProfile;

        /// <summary>
        /// P2 #14 Remediation & Master Budget Table Gate:
        /// Quest 2 / Quest Pro = Max 4 Active Pets
        /// Quest 3 / Quest 3S  = Max 6 Active Pets (All 6 Compute-Skinned Pets < 1.0 ms GPU!)
        /// </summary>
        public int MaxAllowedActivePets
        {
            get
            {
                if (IslandRemediationLightingAndSaveManager.Instance == null) return 4;
                var tier = IslandRemediationLightingAndSaveManager.Instance.DetectedTier;
                return (tier == RemediatedHardwareTier.Quest3 ||
                        tier == RemediatedHardwareTier.Quest3S ||
                        tier == RemediatedHardwareTier.DesktopEditor)
                    ? 6
                    : 4;
            }
        }

        public event Action<IslandPetController> OnPetSpawnedOntoIsland;
        public event Action<IslandPetController> OnPetMovedToSanctuaryRest;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            GameObject rootGo = new GameObject("Active_Island_Pets_Root");
            _petsContainerRoot = rootGo.transform;

            if (archetypeDatabase != null && archetypeDatabase.Archetypes != null && archetypeDatabase.Archetypes.Length > 0)
            {
                _workingStudioProfile = archetypeDatabase.Archetypes[0].DefaultAppearance;
            }
        }

        private void Start()
        {
            if (spawnStarterCompanionsOnStart && archetypeDatabase != null)
            {
                // Spawn a friendly starter Dog and Kitsune Fox so they can immediately play together in Phase 11!
                SpawnCustomizedPet(archetypeDatabase.Archetypes[0].DefaultAppearance, new Vector3(-1.8f, 0f, 2.2f));
                if (archetypeDatabase.Archetypes.Length > 2)
                {
                    SpawnCustomizedPet(archetypeDatabase.Archetypes[2].DefaultAppearance, new Vector3(1.8f, 0f, 2.2f));
                }
            }
        }

        // --- Sanctuary Studio UI Customization API ---

        public void SelectStudioArchetype(int archetypeIndex)
        {
            if (archetypeDatabase == null || archetypeDatabase.Archetypes == null) return;
            int idx = Mathf.Clamp(archetypeIndex, 0, archetypeDatabase.Archetypes.Length - 1);
            _workingStudioProfile = archetypeDatabase.Archetypes[idx].DefaultAppearance;
            RefreshStudioMannequin();
        }

        public void SetStudioBlendShape(int blendShapeIndex, float weight0To100)
        {
            float w = Mathf.Clamp(weight0To100, 0f, 100f);
            switch (blendShapeIndex)
            {
                case 0: _workingStudioProfile.EarDroopOrCrestWeight = w; break;
                case 1: _workingStudioProfile.SnoutLengthWeight = w; break;
                case 2: _workingStudioProfile.TailFluffOrWingWeight = w; break;
                case 3: _workingStudioProfile.BodyChubbinessWeight = w; break;
            }
            RefreshStudioMannequin();
        }

        public void SetStudioCoatColors(Color primary, Color secondaryBelly, Color accentSpots, CoatPatternStyle style)
        {
            _workingStudioProfile.PrimaryCoatColor = primary;
            _workingStudioProfile.SecondaryBellyColor = secondaryBelly;
            _workingStudioProfile.AccentSpotSockColor = accentSpots;
            _workingStudioProfile.PatternStyle = style;
            RefreshStudioMannequin();
        }

        public void SetStudioPetName(string customName)
        {
            _workingStudioProfile.CustomPetName = string.IsNullOrWhiteSpace(customName) ? "Buddy" : customName.Trim();
            RefreshStudioMannequin();
        }

        public IslandPetController AdoptCurrentStudioPetOntoIsland()
        {
            return SpawnCustomizedPet(_workingStudioProfile, defaultPetSpawnPos);
        }

        /// <summary>
        /// Spawns a customized pet onto the 1-acre island while strictly enforcing the P2 #14 Active Pet Hard Cap
        /// (4 on Quest 2/Pro, 6 on Quest 3/3S).
        /// </summary>
        public IslandPetController SpawnCustomizedPet(PetCustomizationProfile profile, Vector3 worldSpawnPos)
        {
            if (archetypeDatabase == null || archetypeDatabase.Archetypes == null) return null;

            int archIdx = Mathf.Clamp((int)profile.Archetype, 0, archetypeDatabase.Archetypes.Length - 1);
            PetArchetypeDefinition def = archetypeDatabase.Archetypes[archIdx];
            if (def.RiggedPrefab == null) return null;

            // Enforce P2 #14 Hard Cap: If at max active pets, move the oldest active pet into Sanctuary Rest
            int maxPets = MaxAllowedActivePets;
            while (_activeSpawnedPets.Count >= maxPets)
            {
                IslandPetController oldestPet = _activeSpawnedPets[0];
                _activeSpawnedPets.RemoveAt(0);
                if (oldestPet != null)
                {
                    _restingSanctuaryPets.Add(ToSavedRecord(oldestPet));
                    OnPetMovedToSanctuaryRest?.Invoke(oldestPet);
                    Destroy(oldestPet.gameObject);
                    Debug.Log($"[P2 #14 Pet Cap] Moved oldest pet to Sanctuary House Rest to maintain {maxPets}-pet hardware ceiling.");
                }
            }

            if (IslandTerrainManager.Instance != null)
            {
                worldSpawnPos.y = Mathf.Max(0f, IslandTerrainManager.Instance.SampleHeightAtWorldPosition(worldSpawnPos));
            }

            GameObject petGo = Instantiate(def.RiggedPrefab, worldSpawnPos, Quaternion.identity, _petsContainerRoot);
            petGo.name = $"Pet_{def.DisplaySpeciesName}_{profile.CustomPetName}";

            IslandPetController controller = petGo.GetComponent<IslandPetController>();
            if (controller != null)
            {
                controller.ApplyCustomizationProfile(profile);
                _activeSpawnedPets.Add(controller);
                OnPetSpawnedOntoIsland?.Invoke(controller);
            }

            return controller;
        }

        public void ExportActivePetsToSaveDocument(IslandSaveDocumentV1 saveDoc)
        {
            if (saveDoc == null) return;
            saveDoc.ActivePets.Clear();
            for (int i = 0; i < _activeSpawnedPets.Count; i++)
            {
                if (_activeSpawnedPets[i] != null)
                {
                    saveDoc.ActivePets.Add(ToSavedRecord(_activeSpawnedPets[i]));
                }
            }
        }

        private static SavedPetRecord ToSavedRecord(IslandPetController pet)
        {
            var p = pet.CurrentProfile;
            return new SavedPetRecord
            {
                ArchetypeId = (int)p.Archetype,
                CustomName = p.CustomPetName,
                CoatColorRgb = new Vector3(p.PrimaryCoatColor.r, p.PrimaryCoatColor.g, p.PrimaryCoatColor.b),
                BlendShapeWeights = new Vector3(p.EarDroopOrCrestWeight, p.SnoutLengthWeight, p.BodyChubbinessWeight),
                Position = pet.transform.position
            };
        }

        private void RefreshStudioMannequin()
        {
            if (studioPreviewMannequin != null)
            {
                studioPreviewMannequin.ApplyCustomizationProfile(_workingStudioProfile);
            }
        }
    }
}
