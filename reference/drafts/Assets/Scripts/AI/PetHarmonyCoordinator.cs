using System.Collections.Generic;
using IslandBuilder.Audio;
using IslandBuilder.Core;
using IslandBuilder.Foliage;
using IslandBuilder.Media;
using IslandBuilder.Pets;
using IslandBuilder.TerrainSystem;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IslandBuilder.AI
{
    public enum PetHarmonyBehaviorState : byte
    {
        PeacefulRoam            = 0,
        SunbatheNearBoxRadio    = 1,
        WatchVirtualCinema      = 2,
        PairedChaseAroundTree   = 3,
        FetchThrownItem         = 4,
        ReturnFetchedItemToUser = 5,
        EnjoyPlayerPetting      = 6
    }

    public struct PetAgentRuntimeState
    {
        public IslandPetController Pet;
        public PetHarmonyBehaviorState Behavior;
        public Vector3 TargetWaypoint;
        public Vector3 LookAtPoint;
        public float MoveSpeedMetersPerSec;
        public float StateExpirationTime;
        public int PairedPartnerIndex; // -1 if solo, otherwise index of partner pet in PairedChaseAroundTree
        public float OrbitAngleRadians;
        public Vector3 OrbitCenterWorldPos;
        public PetFetchableToy TargetFetchToy;
    }

    public sealed class PetHarmonyCoordinator : MonoBehaviour
    {
        public static PetHarmonyCoordinator Instance { get; private set; }

        private const float BlackboardTickIntervalSeconds = 0.25f; // Strict 4 Hz Utility Tick
        private const int MaxTrackedPets = 8;

        private static readonly ProfilerMarker AiBlackboardProfilerMarker =
            new ProfilerMarker("IslandBuilder.Phase11.PetHarmony4HzTick");

        [Header("Player & Toy References")]
        [SerializeField] private Transform playerHeadTransform;
        [SerializeField] private PetFetchableToy defaultIslandFetchBall;

        [Header("Cooperative Social Slot Radii")]
        [SerializeField] private float radioLoungeSlotRadiusMeters = 2.1f;
        [SerializeField] private float treeTagOrbitRadiusMeters = 2.4f;

        private readonly PetAgentRuntimeState[] _agentStates = new PetAgentRuntimeState[MaxTrackedPets];
        private int _agentCount;
        private float _nextBlackboardTickTime;
        private float _lastAiTickDurationMs;

        // Shared 4 Hz Blackboard Snapshot
        private bool _blackboardHasActiveRadio;
        private Vector3 _blackboardRadioPos;
        private bool _blackboardHasActiveCinema;
        private Vector3 _blackboardCinemaPos;
        private Vector3 _blackboardTreeTagCenter = new Vector3(4.5f, 0f, 4.5f);
        private PetFetchableToy _pendingFetchToy;

        public float LastAiTickDurationMs => _lastAiTickDurationMs;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (playerHeadTransform == null && Camera.main != null)
            {
                playerHeadTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            PetFetchableToy.OnToyThrownByPlayer += HandleToyThrownByPlayer;
        }

        private void OnDisable()
        {
            PetFetchableToy.OnToyThrownByPlayer -= HandleToyThrownByPlayer;
        }

        private void HandleToyThrownByPlayer(PetFetchableToy toy)
        {
            if (toy == null || toy.IsClaimedByPet) return;
            _pendingFetchToy = toy;
            // Trigger immediate 4 Hz blackboard evaluation so the nearest pet reacts right away
            _nextBlackboardTickTime = 0f;
        }

        /// <summary>
        /// Called by VRComfortAndHandTrackingManager when the player physically pets a companion with their VR hand.
        /// </summary>
        public void NotifyPlayerPettingCompanion(IslandPetController pet)
        {
            for (int i = 0; i < _agentCount; i++)
            {
                if (_agentStates[i].Pet == pet)
                {
                    var state = _agentStates[i];
                    state.Behavior = PetHarmonyBehaviorState.EnjoyPlayerPetting;
                    state.MoveSpeedMetersPerSec = 0f;
                    state.StateExpirationTime = Time.time + 4.0f;
                    if (playerHeadTransform != null)
                    {
                        state.LookAtPoint = playerHeadTransform.position;
                    }
                    _agentStates[i] = state;
                    return;
                }
            }
        }

        private void Update()
        {
            HandleEditorQuickFetchThrowShortcut();

            if (Time.unscaledTime >= _nextBlackboardTickTime)
            {
                _nextBlackboardTickTime = Time.unscaledTime + BlackboardTickIntervalSeconds;
                float startRt = Time.realtimeSinceStartup;

                using (AiBlackboardProfilerMarker.Auto())
                {
                    SyncActivePetListFromSanctuary();
                    RefreshSharedBlackboardStimuli();
                    EvaluateBlackboardAndUtilityScoresAt4Hz();
                }

                _lastAiTickDurationMs = (Time.realtimeSinceStartup - startRt) * 1000.0f;
            }

            // Smooth zero-GC kinematic steering between 4 Hz ticks (< 0.08 ms for 6 pets)
            StepSmoothPetKinematics(Time.deltaTime);
        }

        private void SyncActivePetListFromSanctuary()
        {
            var sanctuary = IslandPetSanctuaryManager.Instance;
            if (sanctuary == null) return;

            var activePets = sanctuary.ActiveSpawnedPets;
            int targetCount = Mathf.Min(activePets.Count, MaxTrackedPets);

            for (int i = 0; i < targetCount; i++)
            {
                if (_agentStates[i].Pet != activePets[i])
                {
                    _agentStates[i] = new PetAgentRuntimeState
                    {
                        Pet = activePets[i],
                        Behavior = PetHarmonyBehaviorState.PeacefulRoam,
                        TargetWaypoint = activePets[i] != null ? activePets[i].transform.position : Vector3.zero,
                        MoveSpeedMetersPerSec = 1.4f,
                        StateExpirationTime = 0f,
                        PairedPartnerIndex = -1
                    };
                }
            }
            _agentCount = targetCount;
        }

        private void RefreshSharedBlackboardStimuli()
        {
            // 1. Check Phase 9 Spatial Box Radio state
            _blackboardHasActiveRadio = false;
            if (SpatialRadioNetworkManager.Instance != null)
            {
                _blackboardHasActiveRadio = SpatialRadioNetworkManager.Instance.TryGetClosestActiveRadioPosition(
                    Vector3.zero, out _blackboardRadioPos);
            }

            // 2. Check Phase 8 Virtual Cinema Screen state
            _blackboardHasActiveCinema = false;
            if (VirtualCinemaManager.Instance != null && VirtualCinemaManager.Instance.CurrentPrimaryCompositorScreen != null)
            {
                var screen = VirtualCinemaManager.Instance.CurrentPrimaryCompositorScreen;
                if (screen.IsPlaying)
                {
                    _blackboardHasActiveCinema = true;
                    _blackboardCinemaPos = screen.transform.position + screen.transform.forward * 2.8f;
                }
            }
        }

        private void EvaluateBlackboardAndUtilityScoresAt4Hz()
        {
            if (_agentCount == 0) return;

            float now = Time.time;

            // Priority 1: If the player threw a Fetch Toy or uploaded .glb item, dispatch the closest available pet!
            if (_pendingFetchToy != null && !_pendingFetchToy.IsClaimedByPet)
            {
                int bestFetcherIdx = -1;
                float bestDistSq = float.MaxValue;

                for (int i = 0; i < _agentCount; i++)
                {
                    var pet = _agentStates[i].Pet;
                    if (pet == null) continue;
                    float dSq = (pet.transform.position - _pendingFetchToy.transform.position).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestFetcherIdx = i;
                    }
                }

                if (bestFetcherIdx >= 0)
                {
                    var fetcher = _agentStates[bestFetcherIdx];
                    fetcher.Behavior = PetHarmonyBehaviorState.FetchThrownItem;
                    fetcher.TargetFetchToy = _pendingFetchToy;
                    fetcher.TargetWaypoint = _pendingFetchToy.transform.position;
                    fetcher.MoveSpeedMetersPerSec = 3.5f;
                    fetcher.StateExpirationTime = now + 14.0f;
                    fetcher.PairedPartnerIndex = -1;
                    _agentStates[bestFetcherIdx] = fetcher;
                }

                _pendingFetchToy = null;
            }

            // Priority 2: Evaluate ongoing states and assign new Utility Behaviors to expired agents
            for (int i = 0; i < _agentCount; i++)
            {
                var state = _agentStates[i];
                if (state.Pet == null) continue;

                // Handle active Fetch Toy pickup & return transitions
                if (state.Behavior == PetHarmonyBehaviorState.FetchThrownItem && state.TargetFetchToy != null)
                {
                    state.TargetWaypoint = state.TargetFetchToy.transform.position;
                    float distToToySq = (state.Pet.transform.position - state.TargetWaypoint).sqrMagnitude;
                    if (distToToySq < 0.65f)
                    {
                        state.TargetFetchToy.AttachToPetMouth(state.Pet.transform);
                        state.Behavior = PetHarmonyBehaviorState.ReturnFetchedItemToUser;
                        state.StateExpirationTime = now + 12.0f;
                    }
                    _agentStates[i] = state;
                    continue;
                }

                if (state.Behavior == PetHarmonyBehaviorState.ReturnFetchedItemToUser && state.TargetFetchToy != null)
                {
                    Vector3 playerPos = playerHeadTransform != null
                        ? new Vector3(playerHeadTransform.position.x, 0f, playerHeadTransform.position.z)
                        : Vector3.zero;
                    state.TargetWaypoint = playerPos + Vector3.forward * 1.1f;

                    if ((state.Pet.transform.position - state.TargetWaypoint).sqrMagnitude < 1.2f)
                    {
                        state.TargetFetchToy.DropAtPlayerFeet(state.Pet.transform.position + state.Pet.transform.forward * 0.4f);
                        state.TargetFetchToy = null;
                        state.Behavior = PetHarmonyBehaviorState.EnjoyPlayerPetting;
                        state.MoveSpeedMetersPerSec = 0f;
                        state.StateExpirationTime = now + 3.0f;
                    }
                    _agentStates[i] = state;
                    continue;
                }

                // Keep current state until timer expires
                if (now < state.StateExpirationTime) continue;

                // Score Utility Options for Pet i:
                //   Option A: Paired Cooperative Chase/Tag around a Tree (if another pet is available to pair)
                //   Option B: Synchronized Sunbathing near Active Box Radio (Social Slot i)
                //   Option C: Watch Active Virtual Cinema Screen (Social Slot i)
                //   Option D: Peaceful Shoreline-Safe Island Roaming
                float scoreRadio = _blackboardHasActiveRadio ? 0.78f : 0.0f;
                float scoreCinema = _blackboardHasActiveCinema ? 0.82f : 0.0f;
                float scorePairedTag = (_agentCount >= 2 && UnityEngine.Random.value > 0.45f) ? 0.85f : 0.20f;

                if (scorePairedTag >= scoreRadio && scorePairedTag >= scoreCinema && TryStartPairedTreeTag(i, now))
                {
                    continue;
                }

                if (scoreCinema > 0.5f || scoreRadio > 0.5f)
                {
                    bool useCinema = scoreCinema >= scoreRadio;
                    Vector3 socialCenter = useCinema ? _blackboardCinemaPos : _blackboardRadioPos;

                    // Assign non-overlapping circular Social Lounge Slot around the Radio or TV
                    float slotAngle = (i / (float)Mathf.Max(1, _agentCount)) * Mathf.PI * 2f;
                    Vector3 slotPos = socialCenter + new Vector3(
                        Mathf.Cos(slotAngle) * radioLoungeSlotRadiusMeters,
                        0f,
                        Mathf.Sin(slotAngle) * radioLoungeSlotRadiusMeters);

                    if (ChunkedDynamicNavMeshManager.Instance != null)
                    {
                        ChunkedDynamicNavMeshManager.Instance.TrySampleShorelineSafeWaypoint(
                            slotPos, state.Pet.CurrentProfile.Archetype, out slotPos);
                    }

                    state.Behavior = useCinema
                        ? PetHarmonyBehaviorState.WatchVirtualCinema
                        : PetHarmonyBehaviorState.SunbatheNearBoxRadio;
                    state.TargetWaypoint = slotPos;
                    state.LookAtPoint = socialCenter;
                    state.MoveSpeedMetersPerSec = 1.5f;
                    state.StateExpirationTime = now + UnityEngine.Random.Range(8.0f, 15.0f);
                    state.PairedPartnerIndex = -1;
                    _agentStates[i] = state;
                }
                else
                {
                    AssignPeacefulRoamWaypoint(ref state, now);
                    _agentStates[i] = state;
                }
            }
        }

        private bool TryStartPairedTreeTag(int initiatorIndex, float now)
        {
            int partnerIndex = -1;
            for (int j = 0; j < _agentCount; j++)
            {
                if (j == initiatorIndex) continue;
                if (_agentStates[j].Pet != null &&
                    _agentStates[j].Behavior == PetHarmonyBehaviorState.PeacefulRoam)
                {
                    partnerIndex = j;
                    break;
                }
            }

            if (partnerIndex < 0) return false;

            var sA = _agentStates[initiatorIndex];
            var sB = _agentStates[partnerIndex];

            float duration = UnityEngine.Random.Range(7.0f, 11.0f);
            Vector3 treeCenter = _blackboardTreeTagCenter;

            sA.Behavior = PetHarmonyBehaviorState.PairedChaseAroundTree;
            sA.PairedPartnerIndex = partnerIndex;
            sA.OrbitCenterWorldPos = treeCenter;
            sA.OrbitAngleRadians = 0f;
            sA.MoveSpeedMetersPerSec = 3.1f;
            sA.StateExpirationTime = now + duration;

            sB.Behavior = PetHarmonyBehaviorState.PairedChaseAroundTree;
            sB.PairedPartnerIndex = initiatorIndex;
            sB.OrbitCenterWorldPos = treeCenter;
            sB.OrbitAngleRadians = Mathf.PI; // Opposite side of the tree trunk!
            sB.MoveSpeedMetersPerSec = 3.1f;
            sB.StateExpirationTime = now + duration;

            _agentStates[initiatorIndex] = sA;
            _agentStates[partnerIndex] = sB;
            return true;
        }

        private static void AssignPeacefulRoamWaypoint(ref PetAgentRuntimeState state, float now)
        {
            Vector2 randDisc = UnityEngine.Random.insideUnitCircle * 14.0f;
            Vector3 candidate = new Vector3(randDisc.x, 0f, randDisc.y);

            if (ChunkedDynamicNavMeshManager.Instance != null &&
                ChunkedDynamicNavMeshManager.Instance.TrySampleShorelineSafeWaypoint(
                    candidate, state.Pet.CurrentProfile.Archetype, out Vector3 safePt))
            {
                candidate = safePt;
            }

            state.Behavior = PetHarmonyBehaviorState.PeacefulRoam;
            state.TargetWaypoint = candidate;
            state.MoveSpeedMetersPerSec = 1.45f;
            state.StateExpirationTime = now + UnityEngine.Random.Range(5.0f, 9.0f);
            state.PairedPartnerIndex = -1;
        }

        private void StepSmoothPetKinematics(float dt)
        {
            var terrain = IslandTerrainManager.Instance;

            for (int i = 0; i < _agentCount; i++)
            {
                var state = _agentStates[i];
                IslandPetController pet = state.Pet;
                if (pet == null) continue;

                Transform petTr = pet.transform;
                Vector3 currentPos = petTr.position;

                // Update Paired Tree Tag Orbit Waypoint continuously for smooth circular chasing
                if (state.Behavior == PetHarmonyBehaviorState.PairedChaseAroundTree)
                {
                    state.OrbitAngleRadians += (state.MoveSpeedMetersPerSec / Mathf.Max(1f, treeTagOrbitRadiusMeters)) * dt;
                    state.TargetWaypoint = state.OrbitCenterWorldPos + new Vector3(
                        Mathf.Cos(state.OrbitAngleRadians) * treeTagOrbitRadiusMeters,
                        0f,
                        Mathf.Sin(state.OrbitAngleRadians) * treeTagOrbitRadiusMeters);
                }

                Vector3 toTargetXZ = new Vector3(
                    state.TargetWaypoint.x - currentPos.x,
                    0f,
                    state.TargetWaypoint.z - currentPos.z);

                float distSq = toTargetXZ.sqrMagnitude;
                bool hasArrived = distSq < 0.08f;

                if (!hasArrived && state.MoveSpeedMetersPerSec > 0.05f)
                {
                    float dist = Mathf.Sqrt(distSq);
                    Vector3 moveDir = toTargetXZ / dist;
                    float step = Mathf.Min(dist, state.MoveSpeedMetersPerSec * dt);
                    currentPos += moveDir * step;

                    // Keep pet grounded on sculpted terrain elevation in O(1) time
                    if (terrain != null)
                    {
                        float minH = state.Pet.CurrentProfile.Archetype == PetSpeciesArchetype.ChillCapybara
                            ? IslandConstants.WaterLevelY - 0.20f
                            : IslandConstants.WaterLevelY + 0.05f;
                        currentPos.y = Mathf.Max(minH, terrain.SampleHeightAtWorldPosition(currentPos));
                    }

                    petTr.position = currentPos;
                    Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                    petTr.rotation = Quaternion.Slerp(petTr.rotation, targetRot, dt * 8.0f);

                    pet.CurrentSpeedNormalized = Mathf.Clamp01(state.MoveSpeedMetersPerSec / 3.4f);
                    pet.IsSunbathingOrSleeping = false;
                    pet.SetHeadLookTarget(Vector3.zero, false);
                }
                else
                {
                    pet.CurrentSpeedNormalized = 0.0f;
                    bool shouldLounge = state.Behavior == PetHarmonyBehaviorState.SunbatheNearBoxRadio ||
                                        state.Behavior == PetHarmonyBehaviorState.WatchVirtualCinema;
                    pet.IsSunbathingOrSleeping = shouldLounge;

                    if (shouldLounge || state.Behavior == PetHarmonyBehaviorState.EnjoyPlayerPetting)
                    {
                        pet.SetHeadLookTarget(state.LookAtPoint, true);
                    }
                    else
                    {
                        pet.SetHeadLookTarget(Vector3.zero, false);
                    }
                }

                _agentStates[i] = state;
            }
        }

        private void HandleEditorQuickFetchThrowShortcut()
        {
            // Press 'F' in Unity Editor to toss the Island Fetch Ball forward from the camera!
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame && defaultIslandFetchBall != null)
            {
                Transform camTr = playerHeadTransform != null ? playerHeadTransform : transform;
                defaultIslandFetchBall.transform.position = camTr.position + camTr.forward * 0.8f;
                defaultIslandFetchBall.ThrowToyWithVelocity(camTr.forward * 7.5f + Vector3.up * 3.2f);
            }
        }
    }
}
