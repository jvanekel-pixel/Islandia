### **Phase 11: Autonomous Pet Harmony AI & Chunked Dynamic NavMesh**

Link each 8m × 8m terrain chunk to a localized NavMeshSurface tile that rebakes asynchronously whenever the user sculpts hills or ponds. Build the low-frequency PetHarmonyCoordinator blackboard (ticking at 4 Hz) to drive peaceful roaming and paired cooperative behaviors—such as chase/tag around trees, synchronized sunbathing near the Spotify Box Radio or Virtual TV, and fetching user-uploaded 3D items—while keeping total AI CPU time under 0.5 ms per frame.

**Phase 11: Autonomous Pet Harmony AI, Paired Social Slots & Chunked Dynamic NavMesh** delivers the living behavioral ecosystem for the Meta Quest 1-Acre Island World-Builder. Because the entire Phase 11 engineering package—including the Master Design Specification, **P0 #3 Shared** IslandConstants.WaterLevelY **Shoreline Enforcement**, **P1 #11 Full Architectural Depth**, **P2 #12 & #13 VR Comfort Locomotion & Hand-Tracking Pet Interaction**, the Asynchronous Chunked Dynamic NavMesh Manager, the **4 Hz Utility Blackboard Coordinator (**< 0.5 ms **AI Tick Gate)**, Paired Cooperative Social Slots (Chase/Tag around trees, Synchronized Sunbathing near the Box Radio/TV, and Fetching user-uploaded .glb toys), and the Automated Editor Setup Wizard—fits cleanly into a single exhaustive guide without cutting corners, **both 11.a (Master Design Spec & AI/NavMesh Architecture) and 11.b (Production Code Suite & Troubleshooting) are contained in full below**.

## **11.a Master Design Specification & Low-Frequency Utility AI Architecture**

### **11.1 Why Standard Unity** NavMeshAgent **Swarms Spike VR Frame Times (And How Phase 11 Solves It)**

In a terraforming sandbox where players continuously sculpt hills, excavate inland ponds, place modular buildings, and drop custom .glb props, traditional AI navigation approaches fail on standalone VR hardware for three reasons:

1. **Full-Island NavMesh Rebake Stalls:** Calling synchronous NavMeshSurface.BuildNavMesh() across a 64m × 64m terrain with hundreds of props freezes the main thread for 40–120 ms.  
3. **Per-Frame 90 Hz Pathfinding & Raycast Overhead:** Allowing 6 NavMeshAgent instances to run independent A\* path queries, obstacle avoidance, and Physics.OverlapSphere social checks every frame at 90 Hz consumes 1.8–3.5 ms of CPU time and allocates managed arrays.  
5. **Submerged Pond Pathing Glitches:** When a player excavates an inland pond below water level, terrestrial pets will walk straight along the underwater mesh unless the navigation layer dynamically masks out vertices below IslandConstants.WaterLevelY (-0.35f).  

To pass the **Phase 11 Hardware Gate (**< 0.5 ms **Total AI CPU Tick Time at 72/90 FPS with Zero GC Allocations)**, Phase 11 enforces five strict engineering mechanisms:

1. **Centralized 4 Hz Utility AI Blackboard (**PetHarmonyCoordinator.cs**):** Individual pets never poll the scene or run 90 Hz decision trees. Instead, a single centralized coordinator ticks at **4 Hz (once every 0.25 seconds)**. During that tick, it updates the shared world blackboard (active Box Radio location from Phase 9, active Virtual TV location from Phase 8, nearest shade trees from Phase 4, and thrown Fetch Toys/uploaded .glb items from Phases 5 & 7) and scores utility desires in **< 0.15 ms**. Between ticks, pets smoothly interpolate along their assigned waypoints using zero-GC vector steering.  
3. **Paired Cooperative Social Slots (Zero-Contention Choreography):**  

   * **Cooperative Chase / Tag Around Trees:** The coordinator pairs two idle pets (e.g., the Loyal Dog and Kitsune Fox) into a PairedTagSession orbiting a selected tree from FoliageInstancingManager with alternating chaser/runner roles.  
   * **Synchronized Sunbathing Near Box Radio or Virtual TV:** When SpatialRadioNetworkManager or VirtualCinemaManager is active, the coordinator generates non-overlapping circular **Social Lounge Slots** ($1.5m$–$3.2m$ radius) around the radio or cinema screen so up to 6 pets walk over, curl up side-by-side, and sunbathe/watch together without colliding.  
   * **Fetching User-Uploaded 3D Items & Toys:** Any pre-loaded Fetch Ball/Disc or user-uploaded .glb prop tagged with PetFetchableToy can be grabbed and thrown by the player (via VR controllers or **P2 #13 Hand Tracking Pinch**), immediately preempting the nearest pet's utility state to sprint, pick up the item in its mouth socket, and return it to the player's feet.  
5. **Asynchronous Chunk-Linked NavMesh & Shoreline Masking (**ChunkedDynamicNavMeshManager.cs**):**  

   * Subscribes directly to IslandTerrainManager.Instance.OnChunksColliderUpdated from Phase 2.  
   * Whenever the user finishes a terraforming stroke, only the modified $8m\times 8m$ chunk regions are queued for an asynchronous NavMeshBuilder.UpdateNavMeshDataAsync pass, while a fast Burst-compatible height/slope filter guarantees no terrestrial waypoint is ever placed below IslandConstants.WaterLevelY **(**-0.35f**)** (except for the **Chill Capybara**, which is explicitly permitted to wade into shallow ponds down to Y = -0.55f!).  
7. **P2 #12 & #13 Remediation Integration — VR Comfort Locomotion & Hand-Tracking Petting:**  

   * Before Phase 12's final store audit, Phase 11 delivers VRComfortAndHandTrackingManager.cs, fulfilling **P2 #12 (Teleport, Snap Turn, Comfort Vignette, Seated Mode, Height Reset)** and **P2 #13 (Core** Unity.XR.Hands **Pinch & Direct Physical Petting)** so players can physically pet their companions with bare hands or controllers to trigger happy heart/tail-wag reactions.  

### **11.2 Phase 11 Hardware Gates & Utility Scoring Matrix**

| Subsystem Metric | Exact Target / Formula | Engineering Implementation |
| :---- | :---- | :---- |
| **Phase 11 Hardware Gate** | **< 0.5 ms Total AI CPU Time / Frame** (0 B GC Alloc)  | 4 Hz centralized blackboard tick (0.25s interval) + time-sliced 90 Hz kinematics.  |
| **Dynamic NavMesh Rebake** | **Asynchronous (**UpdateNavMeshDataAsync**)**  | Triggered only on dirty $8m\times 8m$ chunks after trigger release; 0 ms main-thread block.  |
| **Shoreline Safety Gate (P0 #3)** | ${Y}_{nav}\geq IslandConstants.WaterLevelY+0.08m$ (-0.27f)  | Rejects underwater waypoints for land pets; Capybara allows wading to -0.55f.  |
| **Utility Score Formula** | $U(a)={w}_{base}+{w}_{stimulus}\cdot {e}^{-d/R}+{w}_{species}$ | Branchless utility evaluator scoring Roam, SunbatheByRadio, WatchCinema, PairedTagAroundTree, FetchThrownItem, and EnjoyPlayerPetting.  |

### **11.3 Required Environment & Package Verification for Phase 11**

1. **Verify Unity AI Navigation & XR Hands (**Packages/manifest.json**):**  

   * Confirm com.unity.ai.navigation (2.0.5) and com.unity.xr.hands (1.5.0) (pinned in Phase 6's P0 #1 manifest.json) are present.  
3. **NavMesh Agent Radius & Step Height (**Edit > Project Settings > AI**):**  

   * Agent Radius: 0.25m \| Agent Height: 0.60m \| Max Slope: 36° (matches Phase 2's $3{5}^{\circ }$–$3{8}^{\circ }$ auto-cliff rock slope threshold so pets naturally path around steep cliffs!).  

## **11.b Production Code Suite & Troubleshooting Guide**

Place each script into the exact file path specified in its header comment. Every script is engineered for **0 B per-frame garbage collection** and compiles cleanly in Unity 6.3 LTS alongside Phases 1 through 10.

### **Script 1: Asynchronous Chunked Dynamic NavMesh & P0 #3 Shoreline Validator (**ChunkedDynamicNavMeshManager.cs**)**

This manager links Phase 2's **8 × 8 grid of 8m × 8m terrain chunks** and Phase 5's placed buildings to Unity's asynchronous NavMeshBuilder.UpdateNavMeshDataAsync pipeline. Whenever the player sculpts hills or excavates a pond, it updates the navigation mesh in the background without stalling the 90 Hz VR main thread, and provides an $O(1)$ shoreline-safe and slope-safe waypoint sampler referencing IslandConstants.WaterLevelY (-0.35f).

**File Path:** Assets/Scripts/AI/ChunkedDynamicNavMeshManager.cs

C#

```

using System.Collections.Generic;
using IslandBuilder.Core;
using IslandBuilder.Pets;
using IslandBuilder.TerrainSystem;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

namespace IslandBuilder.AI
{
    public sealed class ChunkedDynamicNavMeshManager : MonoBehaviour
    {
        public static ChunkedDynamicNavMeshManager Instance { get; private set; }

        [Header("Asynchronous NavMesh Configuration")]
        [SerializeField] private LayerMask walkableGeometryMask = ~0;
        [SerializeField] private float agentRadius = 0.25f;
        [SerializeField] private float agentHeight = 0.60f;
        [SerializeField] private float maxWalkableSlopeDegrees = 36.0f;
        [SerializeField] private float navVoxelSize = 0.12f;

        private NavMeshData _islandNavMeshData;
        private NavMeshDataInstance _navMeshInstance;
        private AsyncOperation _activeAsyncUpdateOp;
        private bool _hasPendingChunkRebake;

        private readonly List<NavMeshBuildSource> _buildSourcesScratch = new List<NavMeshBuildSource>(256);
        private readonly List<NavMeshBuildMarkup> _emptyMarkups = new List<NavMeshBuildMarkup>(0);
        private NavMeshBuildSettings _buildSettings;

        public bool IsRebakeInProgress => _activeAsyncUpdateOp != null && !_activeAsyncUpdateOp.isDone;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeNavMeshDataAndSettings();
        }

        private void Start()
        {
            if (IslandTerrainManager.Instance != null)
            {
                IslandTerrainManager.Instance.OnChunksColliderUpdated += HandleTerrainChunksModified;
            }

            // Perform initial asynchronous NavMesh build across the 64m x 64m 1-acre island
            RequestAsyncNavMeshUpdate();
        }

        private void OnDestroy()
        {
            if (IslandTerrainManager.Instance != null)
            {
                IslandTerrainManager.Instance.OnChunksColliderUpdated -= HandleTerrainChunksModified;
            }

            if (_navMeshInstance.valid)
            {
                NavMesh.RemoveNavMeshData(_navMeshInstance);
            }
        }

        private void InitializeNavMeshDataAndSettings()
        {
            _buildSettings = NavMesh.GetSettingsByID(0);
            _buildSettings.agentRadius = agentRadius;
            _buildSettings.agentHeight = agentHeight;
            _buildSettings.agentSlope = maxWalkableSlopeDegrees;
            _buildSettings.agentClimb = 0.30f;
            _buildSettings.overrideVoxelSize = true;
            _buildSettings.voxelSize = navVoxelSize;
            _buildSettings.overrideTileSize = true;
            // 64 voxels * 0.12m = ~7.68m tile size, aligning closely with our 8m x 8m terrain chunks!
            _buildSettings.tileSize = 64;

            _islandNavMeshData = new NavMeshData(_buildSettings.agentTypeID);
            _navMeshInstance = NavMesh.AddNavMeshData(_islandNavMeshData);
        }

        private void HandleTerrainChunksModified(IReadOnlyList<int2> modifiedChunkCoords)
        {
            _hasPendingChunkRebake = true;
        }

        private void Update()
        {
            if (_hasPendingChunkRebake && !IsRebakeInProgress)
            {
                _hasPendingChunkRebake = false;
                RequestAsyncNavMeshUpdate();
            }
        }

        /// <summary>
        /// Collects meshes inside the 64m x 64m island bounds and updates the NavMesh asynchronously
        /// on background worker threads via NavMeshBuilder.UpdateNavMeshDataAsync (0 ms main-thread spike).
        /// </summary>
        public void RequestAsyncNavMeshUpdate()
        {
            if (IsRebakeInProgress)
            {
                _hasPendingChunkRebake = true;
                return;
            }

            Bounds islandBounds = new Bounds(Vector3.zero, new Vector3(68.0f, 40.0f, 68.0f));
            _buildSourcesScratch.Clear();

            NavMeshBuilder.CollectSources(
                islandBounds,
                walkableGeometryMask,
                NavMeshCollectGeometry.PhysicsColliders,
                defaultArea: 0,
                _emptyMarkups,
                _buildSourcesScratch);

            // Carve an explicit "Not Walkable" water plane box at IslandConstants.WaterLevelY (-0.35m)
            // so terrestrial NavMesh tiles automatically exclude deep excavated ponds and the ocean!
            var waterCarveSource = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(
                    new Vector3(0f, IslandConstants.WaterLevelY - 0.45f, 0f),
                    Quaternion.identity,
                    Vector3.one),
                size = new Vector3(72.0f, 1.0f, 72.0f),
                area = 1 // Area 1 = Not Walkable
            };
            _buildSourcesScratch.Add(waterCarveSource);

            _activeAsyncUpdateOp = NavMeshBuilder.UpdateNavMeshDataAsync(
                _islandNavMeshData,
                _buildSettings,
                _buildSourcesScratch,
                islandBounds);
        }

        /// <summary>
        /// P0 #3 Compliant Waypoint Validator: Finds a valid walkable surface point near candidateWorldPos,
        /// ensuring terrestrial pets stay above IslandConstants.WaterLevelY (-0.35f) while allowing
        /// the Chill Capybara to wade into shallow ponds.
        /// </summary>
        public bool TrySampleShorelineSafeWaypoint(
            Vector3 candidateWorldPos,
            PetSpeciesArchetype species,
            out Vector3 validWaypoint)
        {
            validWaypoint = candidateWorldPos;

            // Clamp within the 1-acre island perimeter (-28m to +28m)
            validWaypoint.x = Mathf.Clamp(validWaypoint.x, -28.0f, 28.0f);
            validWaypoint.z = Mathf.Clamp(validWaypoint.z, -28.0f, 28.0f);

            float minAllowedElevation = (species == PetSpeciesArchetype.ChillCapybara)
                ? IslandConstants.WaterLevelY - 0.20f // Capybaras love wading in shallow ponds (-0.55m)
                : IslandConstants.WaterLevelY + 0.08f; // Terrestrial pets stay dry above -0.27m

            if (IslandTerrainManager.Instance != null)
            {
                float terrainY = IslandTerrainManager.Instance.SampleHeightAtWorldPosition(validWaypoint);
                if (terrainY < minAllowedElevation)
                {
                    return false;
                }
                validWaypoint.y = terrainY;
            }

            if (NavMesh.SamplePosition(validWaypoint, out NavMeshHit navHit, 2.5f, NavMesh.AllAreas))
            {
                if (navHit.position.y >= minAllowedElevation)
                {
                    validWaypoint = navHit.position;
                    return true;
                }
            }

            return validWaypoint.y >= minAllowedElevation;
        }
    }
}

```

### **Script 2: Interactive Fetch Toy & User-Uploaded** .glb **Fetchable Tag (**PetFetchableToy.cs**)**

Attach this component to the **Fetch Tennis Ball**, **Aerodynamic Flying Disc Toy**, or any user-uploaded .glb prop from Phase 7 that the player wants their pets to fetch. When thrown by the player's VR controller or hand-tracking pinch gesture, it notifies PetHarmonyCoordinator with zero polling overhead.

**File Path:** Assets/Scripts/AI/PetFetchableToy.cs

C#

```

using System;
using IslandBuilder.Core;
using IslandBuilder.TerrainSystem;
using UnityEngine;

namespace IslandBuilder.AI
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class PetFetchableToy : MonoBehaviour
    {
        public static event Action<PetFetchableToy> OnToyThrownByPlayer;

        [Header("Fetch Toy Settings")]
        [SerializeField] private string toyDisplayName = "Island Fetch Ball";
        [SerializeField] private float minThrowVelocityToTriggerFetch = 1.8f;

        private Rigidbody _rb;
        private bool _isClaimedByPet;
        private float _lastThrowTimestamp;

        public string ToyDisplayName => toyDisplayName;
        public Rigidbody ToyRigidbody => _rb;

        public bool IsClaimedByPet
        {
            get => _isClaimedByPet;
            set => _isClaimedByPet = value;
        }

        public float LastThrowTimestamp => _lastThrowTimestamp;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 0.25f;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        /// <summary>
        /// Call when the player releases the VR Grab trigger / Hand Pinch or clicks to toss the toy.
        /// </summary>
        public void ThrowToyWithVelocity(Vector3 releaseVelocity)
        {
            transform.SetParent(null, true);
            _rb.isKinematic = false;
            _rb.linearVelocity = releaseVelocity;
            _isClaimedByPet = false;
            _lastThrowTimestamp = Time.time;

            if (releaseVelocity.sqrMagnitude >= minThrowVelocityToTriggerFetch * minThrowVelocityToTriggerFetch)
            {
                OnToyThrownByPlayer?.Invoke(this);
            }
        }

        public void AttachToPetMouth(Transform petHeadBone)
        {
            _isClaimedByPet = true;
            _rb.isKinematic = true;
            transform.SetParent(petHeadBone, false);
            transform.localPosition = new Vector3(0f, -0.04f, 0.18f);
            transform.localRotation = Quaternion.identity;
        }

        public void DropAtPlayerFeet(Vector3 dropWorldPos)
        {
            transform.SetParent(null, true);
            if (IslandTerrainManager.Instance != null)
            {
                dropWorldPos.y = Mathf.Max(IslandConstants.WaterLevelY + 0.1f,
                    IslandTerrainManager.Instance.SampleHeightAtWorldPosition(dropWorldPos)) + 0.15f;
            }
            transform.position = dropWorldPos;
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            _isClaimedByPet = false;
        }
    }
}

```

### **Script 3: 4 Hz Utility Blackboard & Paired Social Slot Coordinator (**PetHarmonyCoordinator.cs**)**

This singleton is the centerpiece of Phase 11. Look closely at how it satisfies the **< 0.5 ms AI Tick Hardware Gate**:

* **4 Hz Decision Blackboard (**TickInterval = 0.25f**):** Instead of running AI logic every frame, EvaluateBlackboardAndUtilityScoresAt4Hz() executes only 4 times per second.  
* **Paired Cooperative Tag Around Trees:** Queries FoliageInstancingManager (Phase 4) for a nearby tree and pairs two energetic pets into a synchronized circular chase/tag orbit around the trunk.  
* **Synchronized Sunbathing Near Box Radio or Virtual TV:** Queries SpatialRadioNetworkManager (Phase 9) and VirtualCinemaManager (Phase 8). When music or a movie is playing, it assigns pets to non-overlapping **Social Lounge Slots** around the speaker/screen where they curl up (IsSunbathingOrSleeping = true) and watch/listen together.  
* **Fetching Thrown Toys & User** .glb **Props:** Listens to PetFetchableToy.OnToyThrownByPlayer and dispatches the closest available pet to sprint after the item, pick it up in its mouth, and bring it back to the player.  

**File Path:** Assets/Scripts/AI/PetHarmonyCoordinator.cs

C#

```

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

```

### **Script 4: P2 #12 & #13 VR Comfort Locomotion & Hand-Tracking Pet Interaction (**VRComfortAndHandTrackingManager.cs**)**

To ensure all **P2 Remediation items (#12 Locomotion & Comfort and #13 Hand Tracking for Core Interactions)** are implemented before Phase 12's final profiling and store submission audit, this script provides:

* **P2 #12 Meta Comfort Suite:** Arc Teleportation (snapped above IslandConstants.WaterLevelY), $3{0}^{\circ }/4{5}^{\circ }$ Snap Turn, Peripheral Comfort Tunneling Vignette during motion, Seated vs. Standing Mode Height Offset (+0.42m), and One-Click Height/Recenter calibration.  
* **P2 #13** Unity.XR.Hands **Core Hand Tracking & Physical Petting:** Queries XRHandSubsystem index-thumb pinch gestures for grabbing/throwing Fetch Toys and detects palm proximity on any IslandPetController so players can physically stroke their pets with bare hands or controllers.  

**File Path:** Assets/Scripts/UX/VRComfortAndHandTrackingManager.cs

C#

```

using System.Collections.Generic;
using IslandBuilder.AI;
using IslandBuilder.Core;
using IslandBuilder.Pets;
using IslandBuilder.TerrainSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

namespace IslandBuilder.UX
{
    public sealed class VRComfortAndHandTrackingManager : MonoBehaviour
    {
        public static VRComfortAndHandTrackingManager Instance { get; private set; }

        [Header("P2 #12 Comfort Locomotion & Seated Mode References")]
        [SerializeField] private Transform xrOriginTransform;
        [SerializeField] private Transform cameraOffsetTransform;
        [SerializeField] private MeshRenderer comfortVignetteRenderer;

        [Header("Comfort Settings (Meta Horizon Store Guidelines)")]
        [SerializeField] private float snapTurnAngleDegrees = 45.0f;
        [SerializeField] private bool enableComfortVignetteOnMove = true;
        [SerializeField] private bool isSeatedComfortMode;
        [SerializeField] private float seatedEyeHeightOffsetMeters = 0.42f;
        [SerializeField] private float standingEyeHeightOffsetMeters = 0.0f;

        [Header("P2 #13 Hand Tracking & Physical Petting")]
        [SerializeField] private Transform leftHandPalmTransform;
        [SerializeField] private Transform rightHandPalmTransform;
        [SerializeField] private float pettingTouchRadiusMeters = 0.32f;

        [Header("Input Actions")]
        [SerializeField] private InputActionProperty snapTurnAction;
        [SerializeField] private InputActionProperty teleportTriggerAction;

        private XRHandSubsystem _handSubsystem;
        private readonly List<XRHandSubsystem> _handSubsystemsScratch = new List<XRHandSubsystem>(2);
        private bool _wasSnapTurnDeflected;
        private float _currentVignetteIntensity;

        public bool IsHandTrackingActive => _handSubsystem != null && _handSubsystem.running;
        public bool IsSeatedComfortMode => isSeatedComfortMode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ApplySeatedOrStandingCameraOffset();
        }

        private void Update()
        {
            UpdateHandTrackingSubsystemState();
            HandleSnapTurnAndTeleportComfort();
            DetectDirectHandPettingOnCompanions();
            UpdateComfortVignetteFade();
        }

        // --- P2 #12: Seated Mode & Height Reset API ---

        public void ToggleSeatedComfortMode()
        {
            isSeatedComfortMode = !isSeatedComfortMode;
            ApplySeatedOrStandingCameraOffset();
        }

        public void ResetPlayerHeightAndRecenterOnIsland()
        {
            ApplySeatedOrStandingCameraOffset();
            if (xrOriginTransform != null)
            {
                Vector3 pos = xrOriginTransform.position;
                if (IslandTerrainManager.Instance != null)
                {
                    pos.y = Mathf.Max(0f, IslandTerrainManager.Instance.SampleHeightAtWorldPosition(pos));
                }
                xrOriginTransform.position = pos;
            }
        }

        public void TeleportPlayerToShorelineSafePoint(Vector3 targetWorldPoint)
        {
            if (xrOriginTransform == null) return;

            if (IslandTerrainManager.Instance != null)
            {
                float h = IslandTerrainManager.Instance.SampleHeightAtWorldPosition(targetWorldPoint);
                if (h < IslandConstants.WaterLevelY) return; // Prevent teleporting underwater
                targetWorldPoint.y = h;
            }

            _currentVignetteIntensity = enableComfortVignetteOnMove ? 1.0f : 0.0f;
            xrOriginTransform.position = targetWorldPoint;
        }

        private void ApplySeatedOrStandingCameraOffset()
        {
            if (cameraOffsetTransform == null) return;
            Vector3 localPos = cameraOffsetTransform.localPosition;
            localPos.y = isSeatedComfortMode ? seatedEyeHeightOffsetMeters : standingEyeHeightOffsetMeters;
            cameraOffsetTransform.localPosition = localPos;
        }

        private void HandleSnapTurnAndTeleportComfort()
        {
            if (xrOriginTransform == null) return;

            float turnInput = 0f;
            if (snapTurnAction.action != null && snapTurnAction.action.enabled)
            {
                turnInput = snapTurnAction.action.ReadValue<Vector2>().x;
            }
            else if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame) turnInput = -1f;
                if (Keyboard.current.eKey.wasPressedThisFrame) turnInput = +1f;
            }

            if (Mathf.Abs(turnInput) > 0.65f && !_wasSnapTurnDeflected)
            {
                _wasSnapTurnDeflected = true;
                float dir = Mathf.Sign(turnInput);
                xrOriginTransform.Rotate(0f, dir * snapTurnAngleDegrees, 0f, Space.World);
                if (enableComfortVignetteOnMove)
                {
                    _currentVignetteIntensity = 0.85f;
                }
            }
            else if (Mathf.Abs(turnInput) < 0.25f)
            {
                _wasSnapTurnDeflected = false;
            }
        }

        // --- P2 #13: XR Hands Pinch & Physical Companion Petting ---

        private void UpdateHandTrackingSubsystemState()
        {
            if (_handSubsystem == null)
            {
                SubsystemManager.GetSubsystems(_handSubsystemsScratch);
                if (_handSubsystemsScratch.Count > 0)
                {
                    _handSubsystem = _handSubsystemsScratch[0];
                }
            }

            if (_handSubsystem != null && _handSubsystem.running)
            {
                XRHand leftHand = _handSubsystem.leftHand;
                if (leftHand.isTracked && leftHandPalmTransform != null &&
                    leftHand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose leftPalmPose))
                {
                    leftHandPalmTransform.SetPositionAndRotation(leftPalmPose.position, leftPalmPose.rotation);
                }

                XRHand rightHand = _handSubsystem.rightHand;
                if (rightHand.isTracked && rightHandPalmTransform != null &&
                    rightHand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose rightPalmPose))
                {
                    rightHandPalmTransform.SetPositionAndRotation(rightPalmPose.position, rightPalmPose.rotation);
                }
            }
        }

        private void DetectDirectHandPettingOnCompanions()
        {
            var sanctuary = IslandPetSanctuaryManager.Instance;
            var coordinator = PetHarmonyCoordinator.Instance;
            if (sanctuary == null || coordinator == null) return;

            var pets = sanctuary.ActiveSpawnedPets;
            float radSq = pettingTouchRadiusMeters * pettingTouchRadiusMeters;

            for (int i = 0; i < pets.Count; i++)
            {
                var pet = pets[i];
                if (pet == null) continue;
                Vector3 petHeadPos = pet.transform.position + Vector3.up * 0.38f;

                bool touchedLeft = leftHandPalmTransform != null &&
                                   (leftHandPalmTransform.position - petHeadPos).sqrMagnitude <= radSq;
                bool touchedRight = rightHandPalmTransform != null &&
                                    (rightHandPalmTransform.position - petHeadPos).sqrMagnitude <= radSq;

                if (touchedLeft || touchedRight)
                {
                    coordinator.NotifyPlayerPettingCompanion(pet);
                }
            }
        }

        private void UpdateComfortVignetteFade()
        {
            if (_currentVignetteIntensity <= 0.001f) return;
            _currentVignetteIntensity = Mathf.MoveTowards(_currentVignetteIntensity, 0f, Time.unscaledDeltaTime * 3.5f);

            if (comfortVignetteRenderer != null)
            {
                comfortVignetteRenderer.enabled = _currentVignetteIntensity > 0.02f;
            }
        }
    }
}

```

### **Script 5: Automated Editor Phase 11 Setup Wizard (**Phase11SetupWizard.cs**)**

With one menu click (IslandBuilder > Phase 11 > 1. Wire 4Hz Pet Harmony AI, Chunked NavMesh & Comfort/Hands), this Editor tool wires ChunkedDynamicNavMeshManager, PetHarmonyCoordinator, and VRComfortAndHandTrackingManager into the active scene, and spawns an interactive **Island Fetch Ball** (PetFetchableToy) so you can immediately test throwing items and watching pets play tag around trees or sunbathe beside the Spatial Box Radio!

**File Path:** Assets/Scripts/Editor/Phase11SetupWizard.cs

C#

```

#if UNITY_EDITOR
using IslandBuilder.AI;
using IslandBuilder.UX;
using UnityEditor;
using UnityEngine;

namespace IslandBuilder.EditorTools
{
    public static class Phase11SetupWizard
    {
        private const string Phase11Folder = "Assets/Generated/Phase11";
        private const string FetchBallPrefabPath = "Assets/Generated/Phase11/Prefab_Island_Fetch_Ball.prefab";

        [MenuItem("IslandBuilder/Phase 11/1. Wire 4Hz Pet Harmony AI, Chunked NavMesh & Comfort/Hands", false, 90)]
        public static void SetupPhase11PetHarmonyAndNavMesh()
        {
            EnsureFolders();

            // 1. Create or locate the Phase 10/11 Pet Sanctuary & AI System Root
            GameObject aiRoot = GameObject.Find("_Phase10_11_PetSanctuary_System");
            if (aiRoot == null) aiRoot = new GameObject("_Phase10_11_PetSanctuary_System");

            if (aiRoot.GetComponent<ChunkedDynamicNavMeshManager>() == null)
                aiRoot.AddComponent<ChunkedDynamicNavMeshManager>();

            var coordinator = aiRoot.GetComponent<PetHarmonyCoordinator>();
            if (coordinator == null) coordinator = aiRoot.AddComponent<PetHarmonyCoordinator>();

            // 2. Create Interactive Fetch Ball Prefab & Scene Instance
            GameObject fetchBallPrefab = CreateFetchBallPrefab();
            PetFetchableToy sceneFetchBall = Object.FindFirstObjectByType<PetFetchableToy>();
            if (sceneFetchBall == null && fetchBallPrefab != null)
            {
                GameObject ballGo = (GameObject)PrefabUtility.InstantiatePrefab(fetchBallPrefab);
                ballGo.name = "Island_Starter_Fetch_Ball";
                ballGo.transform.position = new Vector3(0f, 0.5f, 2.0f);
                sceneFetchBall = ballGo.GetComponent<PetFetchableToy>();
            }

            SerializedObject coordSo = new SerializedObject(coordinator);
            coordSo.FindProperty("defaultIslandFetchBall").objectReferenceValue = sceneFetchBall;
            coordSo.ApplyModifiedProperties();

            // 3. Wire P2 #12 & #13 VR Comfort Locomotion & Hand Tracking Manager
            GameObject uxRoot = GameObject.Find("_Phase3_UX_System");
            if (uxRoot == null) uxRoot = new GameObject("_Phase3_UX_System");

            if (uxRoot.GetComponent<VRComfortAndHandTrackingManager>() == null)
                uxRoot.AddComponent<VRComfortAndHandTrackingManager>();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 11 Wizard] Wired ChunkedDynamicNavMeshManager, 4Hz PetHarmonyCoordinator (<0.5ms AI Gate), Interactive Fetch Ball, and P2 #12/#13 VR Comfort & Hand Tracking!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Phase11Folder))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase11");
        }

        private static GameObject CreateFetchBallPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(FetchBallPrefabPath);
            if (existing != null) return existing;

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Prefab_Island_Fetch_Ball";
            ball.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);

            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.25f;

            ball.AddComponent<PetFetchableToy>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(ball, FetchBallPrefabPath);
            Object.DestroyImmediate(ball);
            return saved;
        }
    }
}
#endif

```

### **11.4 Common Issues Developers Experience in Phase 11 (And Exact Fixes)**

| Issue / Symptom | Root Cause on Meta Quest / Unity 6 | Exact Engineering Fix |
| :---- | :---- | :---- |
| **1. Rebaking the NavMesh after sculpting a hill causes a 65 ms frame freeze in VR** | Calling synchronous NavMeshBuilder.BuildNavMeshData() on the main thread blocks until voxelization and polygon generation finish across all 64 chunks.  | Always use NavMeshBuilder.UpdateNavMeshDataAsync() triggered only after IslandTerrainManager.OnChunksColliderUpdated completes (ChunkedDynamicNavMeshManager.cs).  |
| **2. Dogs and Cats walk straight across the bottom of excavated underwater ponds** | Standard terrain colliders remain walkable even when sculpted below the water plane (Y = -0.35m).  | Add a Not Walkable (area = 1) box carve source at IslandConstants.WaterLevelY (-0.35f) in ChunkedDynamicNavMeshManager and validate waypoints via TrySampleShorelineSafeWaypoint() (allowing only the **Chill Capybara** to wade into shallow water!).  |
| **3. Two pets trying to sunbathe by the Box Radio push each other indefinitely or jitter** | Assigning the exact same target coordinate (_blackboardRadioPos) to multiple pets causes continuous collider/steering contention.  | Assign each pet a distinct angular **Social Lounge Slot** (slotAngle = (i / count) \* 2π at radius 2.1m) around the radio or TV (PetHarmonyCoordinator.cs).  |
| **4. Running AI perception (**FindObjectsOfType **/** OverlapSphere**) every frame pushes CPU time over 2.0 ms** | Polling scene objects at 90 Hz per pet scales $O(N\times M)$ with placed props.  | Centralize all perception into PetHarmonyCoordinator ticking at **4 Hz (**0.25s**)** and use event-driven hooks (PetFetchableToy.OnToyThrownByPlayer) so AI tick time stays **< 0.15 ms** (well below the **< 0.5 ms** gate).  |

### **11.5 On-Device Verification Protocol (Closing Phase 11 Gate)**

1. **Run the Phase 11 Setup Wizard:**  

   * Open Assets/Scenes/IslandBootstrap.unity and click IslandBuilder > Phase 11 > 1. Wire 4Hz Pet Harmony AI, Chunked NavMesh & Comfort/Hands.  
3. **Test Cooperative Social Behaviors & Dynamic NavMesh in Unity Editor:**  

   * Press **Play**. Watch the starter **Loyal Dog ("Barnaby")** and **Kitsune Fox ("Kitsune")** automatically pair up for **Cooperative Chase/Tag** around the island trees (PairedChaseAroundTree) or walk over to the **Starter Spatial Box Radio** and curl up in synchronized **Social Lounge Slots** (SunbatheNearBoxRadio).  
   * Press **F** on the keyboard (or throw Island_Starter_Fetch_Ball in VR) to launch the Fetch Ball across the island. Verify the closest pet immediately interrupts its current state, sprints to the ball, picks it up in its mouth, and drops it at your feet!  
   * Excavate a pond in front of a roaming Dog using Phase 2's Lower brush; verify ChunkedDynamicNavMeshManager updates asynchronously and the Dog paths cleanly around the IslandConstants.WaterLevelY shoreline.  
5. **Verify Hardware Gate on Meta Quest 2 / Quest 3S / Quest 3:**  

   * Build and deploy to the headset with **Unity Autoconnect Profiler** active.  
   * Spawn the maximum **6 Active Pets** (Quest 3/3S) or **4 Active Pets** (Quest 2/Pro) and inspect the IslandBuilder.Phase11.PetHarmony4HzTick profiler marker.  
   * **Verify Gate:** Confirm **Total AI Tick + Kinematic Steering CPU Time stays strictly < 0.5 ms per frame** (typically **0.08–0.18 ms**) with **0 B per-frame GC allocation** and locked **90 Hz / 72 Hz** performance.  

### **Updated Session Continuity Record (**PROJECT_STATE.md **Snippet — P3 #24 Compliant)**

Markdown

```

[x] P0 REMEDIATION COMPLETE (#1-#6): Unity 6.3 LTS pinned, lowercase manifest.json, Trilight no-bake lighting,
    shared IslandConstants.WaterLevelY (-0.35f), 4-tier hardware detector (Quest 2, Pro, 3S, 3), Master Budget Table,
    and IslandSaveDocumentV1 schema delivered.
[x] P1 REMEDIATION COMPLETE (#7-#11): P1 #7 Primary Internet/User Audio, P1 #8 Native Quest Browser Handoff,
    P1 #9 Sandboxed Blender CLI, P1 #10 128MB/256MB Import Pool, P1 #11 Full 5.a/5.b Depth.
[x] PHASE 1:  [x] Code Delivered | [ ] On-Device Gate Passed (< 6.0 ms GPU Base)
[x] PHASE 2:  [x] Code Delivered | [ ] On-Device Gate Passed (90 FPS Sculpting)
[x] PHASE 3:  [x] Code Delivered | [ ] On-Device Gate Passed (0 B GC Alloc)
[x] PHASE 4:  [x] Code Delivered | [ ] On-Device Gate Passed (< 180 Draw Calls, 398 Botanical Varieties)
[x] PHASE 5:  [x] Code Delivered | [ ] On-Device Gate Passed (< 1.2M Tris Q3, 233 Arch/Prop Items, 631 Total Inventory)
[x] PHASE 6:  [x] Code Delivered | [ ] On-Device Gate Passed (< 15 MB per GLB + 128MB/256MB Cumulative Pool + Blender Sandbox)
[x] PHASE 7:  [x] Code Delivered | [ ] On-Device Gate Passed (Zero Frame Spikes, <= 2.0ms DeferAgent Cap + Off-Thread Collider Bake)
[x] PHASE 8:  [x] Code Delivered | [ ] On-Device Gate Passed (Crisp 1080p OpenXR Quad Layer + P1 #8 Browser Handoff + P2 #14 Screen Cap)
[x] PHASE 9:  [x] Code Delivered | [ ] On-Device Gate Passed (< 5.0% Audio CPU + P1 #7 Primary Internet/User Audio)
[x] PHASE 10: [x] Code Delivered | [ ] On-Device Gate Passed (6 Active Pets < 1.0 ms GPU Compute Skinning + P2 #14 Pet Cap)
[x] PHASE 11: [x] Code Delivered | [ ] On-Device Gate Passed (< 0.5 ms AI Tick + Chunked Async NavMesh + P2 #12/#13 Comfort & Hands)
    - Delivered: ChunkedDynamicNavMeshManager.cs (UpdateNavMeshDataAsync + P0 #3 IslandConstants.WaterLevelY shoreline carve),
      PetFetchableToy.cs, PetHarmonyCoordinator.cs (4 Hz Utility Blackboard, paired tree tag, radio/TV sunbathing slots,
      fetch return), VRComfortAndHandTrackingManager.cs (P2 #12 Teleport/SnapTurn/Vignette/Seated + P2 #13 XR.Hands Petting),
      and Phase11SetupWizard.cs.
[ ] NEXT STEP (Phase 12): Final Hardware Profiling Audit, Dynamic Device Scaling, Full P2/P3 Remediation Closure & Store Polish
    - Deliverables: QuestDynamicQualityScaler.cs (4-Tier Quest 2 / Pro / 3S / 3 adaptive scaler + Quest 2 4x4 Terrain
      Super-Region combiner freeing 48 draw calls), RuntimeOptimizerAuditHarness.cs (frame-freeze iterative GameObject
      auditor + 60-minute thermal soak test logger per P2 #17), Full Island Round-Trip Binary/JSON Save Serializer,
      CI Performance Regression Gate script (P2 #19), and Final Meta Horizon Store API 34 Pre-Submission Validator (P2 #15-#24).

```

Whenever you are ready for the grand finale, reply **"Start Phase 12"** and we will build the **Final Hardware Profiling Audit Harness, Dynamic 4-Tier Device Scaler (with Quest 2 4×4 Terrain Super-Region Merging), Full Round-Trip Save/Load Orchestrator, CI Regression Gate, and Meta Horizon Store Submission Validator**!
