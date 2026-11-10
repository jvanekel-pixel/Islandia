using System;
using System.Collections.Generic;
using IslandBuilder.TerrainSystem;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IslandBuilder.UX
{
    public enum ToyboxToolType : byte
    {
        GrabAndPlaceBuilding = 0,
        ScatterAndGrowFoliage = 1
    }

    [Serializable]
    public struct PlaceableCatalogItem
    {
        public string ItemId;
        public GameObject Prefab;
        public float FoundationRadiusMeters;
        public bool AutoLevelFoundationOnHills;
        public bool IsBotanicalCluster;
        public float MinScatterSpacingMeters;
    }

    public sealed class ToyboxInteractionController : MonoBehaviour
    {
        [Header("Ray & Preview References")]
        [SerializeField] private Transform controllerRayOrigin;
        [SerializeField] private LayerMask placementSurfaceMask = ~0;
        [SerializeField] private Transform placementGhostTransform;

        [Header("Toybox Mode Configuration")]
        [SerializeField] private ToyboxToolType activeToyboxTool = ToyboxToolType.GrabAndPlaceBuilding;
        [SerializeField] private PlaceableCatalogItem[] catalogItems;
        [SerializeField] private int selectedCatalogIndex;

        [Header("Scatter & Grow Botanical Optimization (15%-20% Size Boost)")]
        [SerializeField] private float scatterBrushRadius = 3.0f;
        [SerializeField] private float scatterHeightScaleMin = 1.15f; // Per VR Archviz Spec: +15% to +20% size
        [SerializeField] private float scatterHeightScaleMax = 1.20f;

        [Header("Input Actions")]
        [SerializeField] private InputActionProperty triggerAction;

        private readonly RaycastHit[] _rayHitsBuffer = new RaycastHit[4];
        private readonly List<Vector3> _recentScatteredPositions = new List<Vector3>(512);

        private bool _wasTriggerPressed;
        private int _activeScatterBatchId;
        private Transform _spawnedItemsRoot;

        /// <summary>
        /// Hook for Phase 4 FoliageInstancingManager so scattered foliage routes directly to GPU Instancing.
        /// </summary>
        public event Action<int, Matrix4x4, Vector4> OnBotanicalInstanceScattered;

        private void Awake()
        {
            GameObject rootGo = new GameObject("Placed_Island_Items_Root");
            _spawnedItemsRoot = rootGo.transform;
        }

        public void SetToyboxTool(ToyboxToolType tool) => activeToyboxTool = tool;

        public void SelectCatalogItem(int index)
        {
            if (catalogItems == null || catalogItems.Length == 0) return;
            selectedCatalogIndex = Mathf.Clamp(index, 0, catalogItems.Length - 1);
        }

        private void Update()
        {
            if (catalogItems == null || catalogItems.Length == 0) return;

            if (!TryRaycastPlacementSurface(out RaycastHit hit))
            {
                if (placementGhostTransform != null && placementGhostTransform.gameObject.activeSelf)
                {
                    placementGhostTransform.gameObject.SetActive(false);
                }
                _wasTriggerPressed = false;
                return;
            }

            UpdatePlacementGhost(hit.point, hit.normal);

            bool triggerPressed = ReadTriggerPressed();

            if (activeToyboxTool == ToyboxToolType.GrabAndPlaceBuilding)
            {
                if (triggerPressed && !_wasTriggerPressed)
                {
                    PlaceSingleItemWithAutoFoundation(hit.point, hit.normal);
                }
            }
            else if (activeToyboxTool == ToyboxToolType.ScatterAndGrowFoliage)
            {
                if (triggerPressed && !_wasTriggerPressed)
                {
                    if (UndoRedoCommandStack.Instance != null)
                    {
                        _activeScatterBatchId = UndoRedoCommandStack.Instance.BeginCommandBatch();
                    }
                }

                if (triggerPressed)
                {
                    ExecuteScatterAndGrowStroke(hit.point);
                }
            }

            _wasTriggerPressed = triggerPressed;
        }

        private void PlaceSingleItemWithAutoFoundation(Vector3 hitPoint, Vector3 hitNormal)
        {
            PlaceableCatalogItem item = catalogItems[selectedCatalogIndex];
            if (item.Prefab == null) return;

            Vector3 spawnPos = hitPoint;

            // Automatic Circular Foundation Pad Leveler when placing Gazebos/Cottages on sloped hills
            if (item.AutoLevelFoundationOnHills && IslandTerrainManager.Instance != null)
            {
                float targetElevation = Mathf.Max(0.0f, hitPoint.y);
                IslandTerrainManager.Instance.LevelCircularFoundationPad(
                    hitPoint,
                    Mathf.Max(1.5f, item.FoundationRadiusMeters),
                    targetElevation);
                spawnPos.y = targetElevation;
            }

            Quaternion spawnRot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            GameObject instance = Instantiate(item.Prefab, spawnPos, spawnRot, _spawnedItemsRoot);

            if (UndoRedoCommandStack.Instance != null)
            {
                UndoRedoCommandStack.Instance.RecordCommand(new IslandCommandRecord
                {
                    ActionType = CommandActionType.SpawnObject,
                    BatchGroupId = UndoRedoCommandStack.Instance.BeginCommandBatch(),
                    TargetObject = instance,
                    AfterState = new TransformSnapshot
                    {
                        Position = spawnPos,
                        Rotation = spawnRot,
                        Scale = instance.transform.localScale
                    }
                });
            }
        }

        private void ExecuteScatterAndGrowStroke(Vector3 strokeCenter)
        {
            PlaceableCatalogItem item = catalogItems[selectedCatalogIndex];
            float minSpacing = Mathf.Max(0.45f, item.MinScatterSpacingMeters);

            // Pick a candidate point inside the brush disc
            Vector2 randomDisc = UnityEngine.Random.insideUnitCircle * scatterBrushRadius;
            Vector3 candidatePos = new Vector3(strokeCenter.x + randomDisc.x, strokeCenter.y, strokeCenter.z + randomDisc.y);

            if (IslandTerrainManager.Instance != null)
            {
                candidatePos.y = IslandTerrainManager.Instance.SampleHeightAtWorldPosition(candidatePos);
            }

            // Do not scatter standard land foliage underwater (unless it is a water lily)
            if (candidatePos.y < -0.25f) return;

            // Zero-GC minimum distance check so plants never overlap and overdraw stays minimal
            float minSpacingSq = minSpacing * minSpacing;
            for (int i = _recentScatteredPositions.Count - 1; i >= 0; i--)
            {
                if ((_recentScatteredPositions[i] - candidatePos).sqrMagnitude < minSpacingSq)
                {
                    return;
                }
            }

            if (_recentScatteredPositions.Count >= 512)
            {
                _recentScatteredPositions.RemoveAt(0);
            }
            _recentScatteredPositions.Add(candidatePos);

            // Apply the VR Archviz optimization rule: Scale bushes & flowers up by 15%-20%
            // while using wider spacing to create lush visual volume with fewer instances!
            float sizeBoost = UnityEngine.Random.Range(scatterHeightScaleMin, scatterHeightScaleMax);
            Vector3 finalScale = Vector3.one * sizeBoost;
            Quaternion finalRot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            // Notify Phase 4 GPU Instanced Foliage Manager if subscribed
            Matrix4x4 trs = Matrix4x4.TRS(candidatePos, finalRot, finalScale);
            OnBotanicalInstanceScattered?.Invoke(selectedCatalogIndex, trs, Vector4.one);

            if (item.Prefab != null)
            {
                GameObject instance = Instantiate(item.Prefab, candidatePos, finalRot, _spawnedItemsRoot);
                instance.transform.localScale = finalScale;

                if (UndoRedoCommandStack.Instance != null)
                {
                    UndoRedoCommandStack.Instance.RecordCommand(new IslandCommandRecord
                    {
                        ActionType = CommandActionType.ScatterBatch,
                        BatchGroupId = _activeScatterBatchId,
                        TargetObject = instance,
                        AfterState = new TransformSnapshot
                        {
                            Position = candidatePos,
                            Rotation = finalRot,
                            Scale = finalScale
                        }
                    });
                }
            }
        }

        private bool TryRaycastPlacementSurface(out RaycastHit closestHit)
        {
            Ray ray;
            if (controllerRayOrigin != null)
            {
                ray = new Ray(controllerRayOrigin.position, controllerRayOrigin.forward);
            }
            else if (Camera.main != null && Mouse.current != null)
            {
                ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            }
            else
            {
                closestHit = default;
                return false;
            }

            int hitCount = Physics.RaycastNonAlloc(ray, _rayHitsBuffer, 250.0f, placementSurfaceMask, QueryTriggerInteraction.Ignore);
            if (hitCount == 0)
            {
                closestHit = default;
                return false;
            }

            int bestIdx = 0;
            float bestDist = _rayHitsBuffer[0].distance;
            for (int i = 1; i < hitCount; i++)
            {
                if (_rayHitsBuffer[i].distance < bestDist)
                {
                    bestDist = _rayHitsBuffer[i].distance;
                    bestIdx = i;
                }
            }

            closestHit = _rayHitsBuffer[bestIdx];
            return true;
        }

        private bool ReadTriggerPressed()
        {
            if (triggerAction.action != null && triggerAction.action.enabled)
            {
                return triggerAction.action.ReadValue<float>() > 0.5f;
            }
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        private void UpdatePlacementGhost(Vector3 pos, Vector3 normal)
        {
            if (placementGhostTransform == null) return;
            if (!placementGhostTransform.gameObject.activeSelf)
            {
                placementGhostTransform.gameObject.SetActive(true);
            }
            placementGhostTransform.position = pos;
            placementGhostTransform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        }
    }
}
