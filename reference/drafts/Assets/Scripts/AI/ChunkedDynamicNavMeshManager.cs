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
