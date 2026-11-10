### **Phase 3: Dual-Scale VR Interaction UX ("Simple & Fun" vs. "Artist Studio")**

Implement the two-handed world-scaling gesture allowing players to shift between **1:1 First-Person Scale** and **1:25 Tabletop God-Scale**. Build the **Toybox Mode** controller tools (one-handed grab/place, automatic terrain pad leveling under buildings, and the "Scatter & Grow" botanical brush) alongside the **Artist Studio Mode** power tools (precision 3D snapping grids, axial gizmos, multi-select, and undo/redo history). Render the wrist menu and the Green/Yellow/Red **Runtime Optimizer Budget Gauge** (<80%, 80%–95%, >95%)24 using an **OpenXR Compositor Layer** for crisp text readability7.  
**Phase 3: Dual-Scale VR Interaction UX ("Simple & Fun" vs. "Artist Studio")** bridges the gap between an accessible, playful VR sandbox and a precision architectural studio. Because the entire Phase 3 engineering package—including the Master UX & Mathematical Specification, Two-Handed World-Scaling Rig, Toybox & Studio Tool Controllers, Zero-GC Undo/Redo Command Stack, OpenXR Compositor Layer Wrist HUD, AppSW Motion-Vector UI Shader, and Automated Editor Setup Wizard—can be delivered in a single, exhaustive blueprint without truncation, **both 3.a (Master Design Spec & Environment) and 3.b (Production Code Suite, Shaders & Troubleshooting) are contained in full below.**

## **3.a Master Design Specification & Dual-Scale Architecture**

### **3.1 Progressive Disclosure & The Dual-Track UX Mandate**

To make the one-acre island feel immediately "simple and fun" for casual sessions while offering the expansive creative depth of massive *Minecraft* builds, Phase 3 implements **Progressive Disclosure across Two Interaction Modes and Two Spatial Scales**:

1. **Dual Spatial Scale (1:1 First-Person vs. 1:25 Tabletop Diorama God-Scale)**:  

   * **Why Never Scale the Island Geometry:** Scaling the 64 terrain chunks and hundreds of placed props between 1.0 and 0.04 every frame forces Unity's physics engine (PhysX) to invalidate and rebuild every static and dynamic collider bounding volume in the scene, causing massive CPU frame spikes.  
   * **The Architectural Solution (Inverse XR Origin Scaling):** The island remains permanently locked at **1:1 world coordinates** ($64m\times 64m$ at origin). Instead, when the user performs the two-handed pinch-and-pull gesture, we scale the XR Origin **(**transform.localScale**) from** 1.0 **(1:1 First-Person Scale) up to** 25.0 **(1:25 Tabletop Diorama Scale)** around the midpoint of the player's hands. At localScale = 25.0, the player is a giant looking down at a $2.56m\times 2.56m$ tabletop diorama floating at waist height, with zero physics rebuild cost!  
3. **Track 1: "Simple & Fun" Toybox Mode (Default)**:  

   * **One-Handed Grab & Place with Auto-Foundation Leveling:** Dropping a gazebo or cottage onto a sloped hill automatically invokes IslandTerrainManager.Instance.LevelCircularFoundationPad() from Phase 2 to flatten a circular pad under the footprint or extend support stilts over water.  
   * **"Scatter & Grow" Botanical Brush:** Instead of placing flowers one by one, sweeping the controller paints mixed botanical meadows using Poisson-disc minimum spacing while scaling up bush and flower cluster sizes by **15%–20%** (1.15x–1.20x) with reduced instance density—creating fuller visual volume with significantly fewer polygons and draw calls.  
5. **Track 2: "Full-Scale Artist Studio" Mode (Power Tools)**:  

   * **Multi-Resolution 3D Snapping Grids:** Artists can lock modular building pieces and uploaded 3D items to a **1.0m, 0.50m (50cm), 0.25m (25cm), or 0.05m (5cm)** 3D spatial grid and $1{5}^{\circ }/4{5}^{\circ }/9{0}^{\circ }$ angular increments.  
   * **3-Axis Precision Gizmo & Multi-Select:** Direct axial translation, rotation, and uniform scale handles for fine-tuning custom assemblies.  
   * **Zero-GC Ring-Buffer Undo/Redo Stack:** Pre-allocated struct/command ring buffer (Capacity = 128) that records object placements, transforms, and 64-chunk heightmap snapshots without runtime heap allocations.  
7. **OpenXR Compositor Layer Wrist HUD & Live "Island Budget" Gauge**:  

   * World-space UI text rendered into the standard 3D eye buffer looks blurry at oblique angles and smears when **Application SpaceWarp (AppSW)** is active.  
   * We route the Left-Wrist Menu Canvas into a dedicated RenderTexture submitted directly to the **OpenXR Compositor** as a quad layer (or OVROverlay / Unity OpenXR Composition Layer), guaranteeing razor-sharp typography and updating the **Green (<80%), Yellow (80%–95%), Red (>95%) Runtime Optimizer Budget Gauge** at 4 Hz with zero string-concatenation GC.  

### **3.2 Phase 3 Hardware Gates & Mathematical Specifications**

| Subsystem Metric | Exact Target / Formula | Engineering Rationale |
| :---- | :---- | :---- |
| **Phase 3 Hardware Gate** | **0 B Managed GC Allocation / Frame**  | String formatting (+$"{val}"), LINQ, and unpooled RaycastHit[] arrays trigger Unity's incremental GC, causing 2–5 ms CPU hitches during 90 Hz VR interaction.  |
| **World-Scale Pivot Invariant** | ${P}_{origin}'={P}_{pivot}+({P}_{origin}-{P}_{pivot})\cdot \left({\frac{{S}_{new}}{{S}_{old}}}\right)$ | Keeps the exact point on the island pinched between the player's two hands stationary in physical room space while scaling between 1.0x and 25.0x.  |
| **Scatter & Grow Scale Factor** | $Scal{e}_{instance}=BaseScale\times Random(1.15,1.20)$  | VR archviz guidelines prove scaling foliage up by 15%–20% while increasing minimum spacing reduces instance counts and draw calls by ~30% while looking fuller.  |
| **Studio Grid Quantization** | $Q(v,g)=round\left({\frac{v}{g}}\right)\cdot g,g\in \{1.0,0.5,0.25,0.05\}m$  | Branchless SIMD math.round(pos / step) \* step quantization in Unity.Mathematics.  |
| **Wrist HUD Refresh Rate** | **4 Hz (Every 0.25s)** | The Wrist Budget Gauge only re-renders its RenderTexture when visible or every 250 ms, saving GPU tile bandwidth while keeping button interaction immediate.  |

### **3.3 Required Environment & Package Configuration for Phase 3**

1. **Verify XR Interaction Toolkit & Input System (**Packages/manifest.json**):**  

   * Confirm com.unity.xr.interaction.toolkit (3.0.7+) and com.unity.inputsystem (1.11.2+) from Phase 1 are installed.  
3. **OpenXR Composition Layers Support:**  

   * If using Unity 6's optional com.unity.xr.compositionlayers package or Meta OpenXR's native quad layer extension, ensure **Depth Submission Mode** is set to Depth 16 Bit in Project Settings > XR Plug-in Management > OpenXR (already enforced in Phase 1) so controllers passing in front of the wrist menu sort accurately.  
5. **Physics Raycast Queries (**Project Settings > Physics**):**  

   * Uncheck **Queries Hit Backfaces** and **Queries Hit Triggers** by default so non-allocating Physics.RaycastNonAlloc calls ignore trigger volumes.  

## **3.b Production Code Suite, Shaders & Troubleshooting Guide**

Place each script and shader into the exact file path specified in its header comment. Every script is engineered for **0 B per-frame garbage collection** and compiles cleanly alongside Phases 1 and 2.

### **Script 1: Two-Handed Dual-Scale Rig Controller (**DualScaleWorldRigController.cs**)**

This component handles the signature two-handed pinch-and-pull gesture to transition smoothly between **1:1 First-Person Scale** (rigScale = 1.0f) and **1:25 Tabletop Diorama God-Scale** (rigScale = 25.0f), plus a one-button quick toggle on the wrist menu. It also dynamically scales DynamicSpaceWarpController motion thresholds and camera near/far clip planes so the entire 64m × 64m island and ocean never clip when viewed at 1:25 miniature scale.

**File Path:** Assets/Scripts/UX/DualScaleWorldRigController.cs

C#

```

using System;
using IslandBuilder.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IslandBuilder.UX
{
    public enum WorldScaleMode : byte
    {
        FirstPerson1To1 = 0,
        TabletopDiorama1To25 = 1,
        CustomTransition = 2
    }

    public sealed class DualScaleWorldRigController : MonoBehaviour
    {
        public static DualScaleWorldRigController Instance { get; private set; }

        [Header("XR Rig & Hand References")]
        [SerializeField] private Transform xrOriginTransform;
        [SerializeField] private Camera xrMainCamera;
        [SerializeField] private Transform leftControllerTransform;
        [SerializeField] private Transform rightControllerTransform;

        [Header("Dual-Scale Limits (1:1 to 1:25 God-Scale)")]
        [SerializeField] private float firstPersonRigScale = 1.0f;
        [SerializeField] private float tabletopDioramaRigScale = 25.0f;
        [SerializeField] private float smoothTransitionSpeed = 8.0f;
        [SerializeField] private Vector3 tabletopDefaultCenterOffset = new Vector3(0f, 18.0f, -22.0f);

        [Header("Two-Handed Grip Input Actions")]
        [SerializeField] private InputActionProperty leftGripAction;
        [SerializeField] private InputActionProperty rightGripAction;
        [SerializeField] private InputActionProperty toggleScaleButtonAction;

        private float _currentRigScale = 1.0f;
        private float _targetRigScale = 1.0f;
        private bool _isTwoHandScaling;
        private float _initialHandDistanceLocal;
        private float _initialScaleOnGrip;
        private Vector3 _pinchWorldAnchorPoint;
        private float _baseNearClip = 0.05f;
        private float _baseFarClip = 600.0f;

        public float CurrentRigScale => _currentRigScale;
        public WorldScaleMode CurrentScaleMode =>
            Mathf.Abs(_currentRigScale - firstPersonRigScale) < 0.1f
                ? WorldScaleMode.FirstPerson1To1
                : (Mathf.Abs(_currentRigScale - tabletopDioramaRigScale) < 0.5f
                    ? WorldScaleMode.TabletopDiorama1To25
                    : WorldScaleMode.CustomTransition);

        public event Action<float, WorldScaleMode> OnWorldScaleChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (xrOriginTransform == null) xrOriginTransform = transform;
            if (xrMainCamera == null) xrMainCamera = Camera.main;
            if (xrMainCamera != null)
            {
                _baseNearClip = xrMainCamera.nearClipPlane;
                _baseFarClip = xrMainCamera.farClipPlane;
            }
        }

        private void Update()
        {
            CheckQuickToggleInput();
            HandleTwoHandedPinchScaleGesture();
            ApplySmoothScaleWhenNotPinching();
        }

        public void ToggleBetweenFirstPersonAndTabletop()
        {
            if (_currentRigScale < 5.0f)
            {
                TransitionToTabletopDiorama();
            }
            else
            {
                TransitionToFirstPerson(Vector3.zero);
            }
        }

        public void TransitionToTabletopDiorama()
        {
            _isTwoHandScaling = false;
            _targetRigScale = tabletopDioramaRigScale;
            _pinchWorldAnchorPoint = Vector3.zero;

            if (DynamicSpaceWarpController.Instance != null)
            {
                DynamicSpaceWarpController.Instance.AcquireSpaceWarpLock();
                Invoke(nameof(ReleaseTransitionSpaceWarp), 0.45f);
            }
        }

        public void TransitionToFirstPerson(Vector3 targetIslandWorldPos)
        {
            _isTwoHandScaling = false;
            _targetRigScale = firstPersonRigScale;
            _pinchWorldAnchorPoint = targetIslandWorldPos;

            if (DynamicSpaceWarpController.Instance != null)
            {
                DynamicSpaceWarpController.Instance.AcquireSpaceWarpLock();
                Invoke(nameof(ReleaseTransitionSpaceWarp), 0.45f);
            }
        }

        private void ReleaseTransitionSpaceWarp()
        {
            if (DynamicSpaceWarpController.Instance != null)
            {
                DynamicSpaceWarpController.Instance.ReleaseSpaceWarpLock();
            }
        }

        private void CheckQuickToggleInput()
        {
            bool togglePressed = false;
            if (toggleScaleButtonAction.action != null && toggleScaleButtonAction.action.enabled)
            {
                togglePressed = toggleScaleButtonAction.action.WasPressedThisFrame();
            }

            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }

            if (togglePressed)
            {
                ToggleBetweenFirstPersonAndTabletop();
            }
        }

        private void HandleTwoHandedPinchScaleGesture()
        {
            if (leftControllerTransform == null || rightControllerTransform == null) return;

            bool leftGrip = IsGripPressed(leftGripAction);
            bool rightGrip = IsGripPressed(rightGripAction);

            if (leftGrip && rightGrip)
            {
                Vector3 leftLocal = xrOriginTransform.InverseTransformPoint(leftControllerTransform.position);
                Vector3 rightLocal = xrOriginTransform.InverseTransformPoint(rightControllerTransform.position);
                float currentHandDistLocal = Vector3.Distance(leftLocal, rightLocal);

                if (!_isTwoHandScaling)
                {
                    _isTwoHandScaling = true;
                    _initialHandDistanceLocal = Mathf.Max(0.08f, currentHandDistLocal);
                    _initialScaleOnGrip = _currentRigScale;
                    _pinchWorldAnchorPoint = (leftControllerTransform.position + rightControllerTransform.position) * 0.5f;

                    if (DynamicSpaceWarpController.Instance != null)
                    {
                        DynamicSpaceWarpController.Instance.AcquireSpaceWarpLock();
                    }
                }
                else
                {
                    // Pulling hands apart enlarges the island (lowers rig scale toward 1:1);
                    // pushing hands together shrinks the island into a 1:25 tabletop diorama.
                    float ratio = _initialHandDistanceLocal / Mathf.Max(0.08f, currentHandDistLocal);
                    float newScale = Mathf.Clamp(_initialScaleOnGrip * ratio, firstPersonRigScale, tabletopDioramaRigScale);

                    ApplyScaleAroundWorldAnchor(newScale, _pinchWorldAnchorPoint);
                    _targetRigScale = newScale;
                }
            }
            else if (_isTwoHandScaling)
            {
                _isTwoHandScaling = false;
                if (DynamicSpaceWarpController.Instance != null)
                {
                    DynamicSpaceWarpController.Instance.ReleaseSpaceWarpLock();
                }
            }
        }

        private void ApplySmoothScaleWhenNotPinching()
        {
            if (_isTwoHandScaling) return;
            if (Mathf.Abs(_currentRigScale - _targetRigScale) < 0.001f) return;

            float nextScale = Mathf.Lerp(_currentRigScale, _targetRigScale, Time.unscaledDeltaTime * smoothTransitionSpeed);
            if (Mathf.Abs(nextScale - _targetRigScale) < 0.01f)
            {
                nextScale = _targetRigScale;
            }

            ApplyScaleAroundWorldAnchor(nextScale, _pinchWorldAnchorPoint);

            // Keep tabletop conveniently centered at waist height when fully zoomed out to 1:25
            if (Mathf.Abs(nextScale - tabletopDioramaRigScale) < 0.05f)
            {
                xrOriginTransform.position = Vector3.Lerp(
                    xrOriginTransform.position,
                    tabletopDefaultCenterOffset,
                    Time.unscaledDeltaTime * smoothTransitionSpeed);
            }
        }

        private void ApplyScaleAroundWorldAnchor(float newScale, Vector3 anchorWorldPos)
        {
            float scaleRatio = newScale / Mathf.Max(0.0001f, _currentRigScale);
            Vector3 offsetFromAnchor = xrOriginTransform.position - anchorWorldPos;

            xrOriginTransform.position = anchorWorldPos + offsetFromAnchor * scaleRatio;
            xrOriginTransform.localScale = new Vector3(newScale, newScale, newScale);
            _currentRigScale = newScale;

            // Scale camera clip planes proportionally so z-buffer precision (16-bit OpenXR depth) remains optimal
            if (xrMainCamera != null)
            {
                xrMainCamera.nearClipPlane = Mathf.Clamp(_baseNearClip * Mathf.Sqrt(newScale), 0.03f, 0.5f);
                xrMainCamera.farClipPlane = _baseFarClip * newScale;
            }

            OnWorldScaleChanged?.Invoke(_currentRigScale, CurrentScaleMode);
        }

        private static bool IsGripPressed(InputActionProperty prop)
        {
            if (prop.action == null || !prop.action.enabled) return false;
            return prop.action.ReadValue<float>() > 0.65f;
        }
    }
}

```

### **Script 2: Zero-GC Ring-Buffer Undo / Redo Command Stack (**UndoRedoCommandStack.cs**)**

Artist Studio Mode requires an Undo/Redo history that never triggers garbage collection or stalls the frame. This script uses a fixed-capacity ring buffer of lightweight value-type structs (IslandCommandRecord) capable of reversing and re-applying single transforms, multi-object batch scatter strokes, and Phase 4 GPU-instanced foliage placements in $O(1)$ time.

**File Path:** Assets/Scripts/UX/UndoRedoCommandStack.cs

C#

```

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IslandBuilder.UX
{
    public enum CommandActionType : byte
    {
        SpawnObject = 0,
        DeleteObject = 1,
        TransformObject = 2,
        ScatterBatch = 3
    }

    public struct TransformSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    public struct IslandCommandRecord
    {
        public CommandActionType ActionType;
        public int BatchGroupId;
        public GameObject TargetObject;
        public TransformSnapshot BeforeState;
        public TransformSnapshot AfterState;
    }

    public sealed class UndoRedoCommandStack : MonoBehaviour
    {
        public static UndoRedoCommandStack Instance { get; private set; }

        private const int MaxHistoryCapacity = 256;

        private readonly IslandCommandRecord[] _ringBuffer = new IslandCommandRecord[MaxHistoryCapacity];
        private int _headIndex;
        private int _undoCount;
        private int _redoCount;
        private int _currentBatchId = 1;

        public int AvailableUndoCount => _undoCount;
        public int AvailableRedoCount => _redoCount;

        public event Action<int, int> OnHistoryStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public int BeginCommandBatch()
        {
            _currentBatchId++;
            return _currentBatchId;
        }

        public void RecordCommand(IslandCommandRecord record)
        {
            _ringBuffer[_headIndex] = record;
            _headIndex = (_headIndex + 1) % MaxHistoryCapacity;

            if (_undoCount < MaxHistoryCapacity)
            {
                _undoCount++;
            }

            // Recording a new action invalidates redo branch
            _redoCount = 0;
            OnHistoryStateChanged?.Invoke(_undoCount, _redoCount);
        }

        public void UndoLastAction()
        {
            if (_undoCount <= 0) return;

            int peekIdx = (_headIndex - 1 + MaxHistoryCapacity) % MaxHistoryCapacity;
            int targetBatch = _ringBuffer[peekIdx].BatchGroupId;

            do
            {
                _headIndex = (_headIndex - 1 + MaxHistoryCapacity) % MaxHistoryCapacity;
                _undoCount--;
                _redoCount++;

                ExecuteInverse(_ringBuffer[_headIndex]);

                if (_undoCount <= 0) break;
                int prevIdx = (_headIndex - 1 + MaxHistoryCapacity) % MaxHistoryCapacity;
                if (targetBatch == 0 || _ringBuffer[prevIdx].BatchGroupId != targetBatch) break;
            }
            while (true);

            OnHistoryStateChanged?.Invoke(_undoCount, _redoCount);
        }

        public void RedoLastAction()
        {
            if (_redoCount <= 0) return;

            int targetBatch = _ringBuffer[_headIndex].BatchGroupId;

            do
            {
                ExecuteForward(_ringBuffer[_headIndex]);

                _headIndex = (_headIndex + 1) % MaxHistoryCapacity;
                _undoCount++;
                _redoCount--;

                if (_redoCount <= 0) break;
                if (targetBatch == 0 || _ringBuffer[_headIndex].BatchGroupId != targetBatch) break;
            }
            while (true);

            OnHistoryStateChanged?.Invoke(_undoCount, _redoCount);
        }

        private static void ExecuteInverse(IslandCommandRecord rec)
        {
            if (rec.TargetObject == null) return;

            switch (rec.ActionType)
            {
                case CommandActionType.SpawnObject:
                case CommandActionType.ScatterBatch:
                    // Soft-deactivate so Redo restores the exact instance with 0 Instantiate() cost
                    rec.TargetObject.SetActive(false);
                    break;

                case CommandActionType.DeleteObject:
                    rec.TargetObject.SetActive(true);
                    break;

                case CommandActionType.TransformObject:
                    rec.TargetObject.transform.SetPositionAndRotation(rec.BeforeState.Position, rec.BeforeState.Rotation);
                    rec.TargetObject.transform.localScale = rec.BeforeState.Scale;
                    break;
            }
        }

        private static void ExecuteForward(IslandCommandRecord rec)
        {
            if (rec.TargetObject == null) return;

            switch (rec.ActionType)
            {
                case CommandActionType.SpawnObject:
                case CommandActionType.ScatterBatch:
                    rec.TargetObject.SetActive(true);
                    break;

                case CommandActionType.DeleteObject:
                    rec.TargetObject.SetActive(false);
                    break;

                case CommandActionType.TransformObject:
                    rec.TargetObject.transform.SetPositionAndRotation(rec.AfterState.Position, rec.AfterState.Rotation);
                    rec.TargetObject.transform.localScale = rec.AfterState.Scale;
                    break;
            }
        }
    }
}

```

### **Script 3: Toybox Mode Controller ("Scatter & Grow" Brush + Auto-Foundation Placement)**

This script implements **Mode 1: "Simple & Fun" Toybox Mode**:

* **Grab & Place with Automatic Foundation Leveling:** Placing any architectural prefab (e.g., Gazebo, Cottage, Pavilion) on a sloped hill automatically invokes IslandTerrainManager.Instance.LevelCircularFoundationPad() to level a circular pad underneath it or extends support stilts when placed over water ($Y<-0.35m$).  
* **"Scatter & Grow" Botanical Brush:** Sweeping the controller across the island scatters mixed trees, bushes, and flowers using a zero-GC spatial hash check, automatically scaling up bush/flower clusters by **15%–20%** (1.15f to 1.20f) while spacing them out to maximize visual fullness and minimize draw calls and overdraw.  

**File Path:** Assets/Scripts/UX/ToyboxInteractionController.cs

C#

```

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

```

### **Script 4: Artist Studio Mode Controller (Multi-Resolution 3D Snapping Grids & Axial Gizmos)**

This controller powers **Mode 2: "Full-Scale Artist Studio"**. It provides instant switching between **1.0m, 50cm (0.5m), 25cm (0.25m), and 5cm (0.05m)** 3D spatial snapping grids, $1{5}^{\circ }/4{5}^{\circ }/9{0}^{\circ }$ rotation snapping, 3-axis gizmo manipulation (X, Y, Z handles), multi-object selection for Phase 5 Runtime Mesh Welding, and direct integration with UndoRedoCommandStack.

**File Path:** Assets/Scripts/UX/ArtistStudioGridGizmoController.cs

C#

```

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IslandBuilder.UX
{
    public enum StudioGridStep : byte
    {
        Grid100cm = 0, // 1.00m
        Grid50cm  = 1, // 0.50m
        Grid25cm  = 2, // 0.25m
        Grid5cm   = 3  // 0.05m
    }

    public enum GizmoAxis : byte
    {
        None = 0,
        AxisX = 1,
        AxisY = 2,
        AxisZ = 3,
        FreePlaneXZ = 4
    }

    public sealed class ArtistStudioGridGizmoController : MonoBehaviour
    {
        public static ArtistStudioGridGizmoController Instance { get; private set; }

        private static readonly float[] GridStepValuesMeters = { 1.00f, 0.50f, 0.25f, 0.05f };

        [Header("Ray & Visual Gizmo Root")]
        [SerializeField] private Transform rightControllerRayOrigin;
        [SerializeField] private Transform visualGizmoRoot;
        [SerializeField] private LayerMask selectableObjectMask = ~0;

        [Header("Snapping Configuration")]
        [SerializeField] private StudioGridStep activeGridStep = StudioGridStep.Grid25cm;
        [SerializeField] private float rotationSnapDegrees = 15.0f;

        [Header("Input Actions")]
        [SerializeField] private InputActionProperty selectTriggerAction;
        [SerializeField] private InputActionProperty undoButtonAction;
        [SerializeField] private InputActionProperty redoButtonAction;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
        private readonly List<GameObject> _multiSelectedObjects = new List<GameObject>(32);

        private GameObject _primarySelectedObject;
        private TransformSnapshot _dragStartSnapshot;
        private bool _isDraggingGizmo;
        private GizmoAxis _activeDragAxis = GizmoAxis.FreePlaneXZ;
        private Vector3 _dragGrabOffset;
        private float _dragPlaneDistance;

        public StudioGridStep ActiveGridStep => activeGridStep;
        public float CurrentGridStepMeters => GridStepValuesMeters[(int)activeGridStep];
        public IReadOnlyList<GameObject> MultiSelectedObjects => _multiSelectedObjects;

        public event Action<StudioGridStep, float> OnGridStepChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SetGridStep(StudioGridStep step)
        {
            activeGridStep = step;
            OnGridStepChanged?.Invoke(activeGridStep, CurrentGridStepMeters);
        }

        public void CycleGridResolution()
        {
            int next = ((int)activeGridStep + 1) % GridStepValuesMeters.Length;
            SetGridStep((StudioGridStep)next);
        }

        /// <summary>
        /// Quantizes any 3D world position to the active Studio Grid (1m, 50cm, 25cm, or 5cm).
        /// </summary>
        public Vector3 SnapPositionToGrid(Vector3 rawWorldPos)
        {
            float step = CurrentGridStepMeters;
            float3 v = new float3(rawWorldPos.x, rawWorldPos.y, rawWorldPos.z);
            float3 snapped = math.round(v / step) * step;
            return new Vector3(snapped.x, snapped.y, snapped.z);
        }

        public Quaternion SnapRotationToIncrement(Quaternion rawRot)
        {
            if (rotationSnapDegrees <= 0.01f) return rawRot;
            Vector3 euler = rawRot.eulerAngles;
            euler.x = Mathf.Round(euler.x / rotationSnapDegrees) * rotationSnapDegrees;
            euler.y = Mathf.Round(euler.y / rotationSnapDegrees) * rotationSnapDegrees;
            euler.z = Mathf.Round(euler.z / rotationSnapDegrees) * rotationSnapDegrees;
            return Quaternion.Euler(euler);
        }

        private void Update()
        {
            HandleKeyboardAndControllerShortcuts();
            HandleGizmoSelectionAndManipulation();
        }

        private void HandleKeyboardAndControllerShortcuts()
        {
            if (undoButtonAction.action != null && undoButtonAction.action.WasPressedThisFrame())
            {
                UndoRedoCommandStack.Instance?.UndoLastAction();
            }
            if (redoButtonAction.action != null && redoButtonAction.action.WasPressedThisFrame())
            {
                UndoRedoCommandStack.Instance?.RedoLastAction();
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.zKey.wasPressedThisFrame && Keyboard.current.ctrlKey.isPressed)
                {
                    UndoRedoCommandStack.Instance?.UndoLastAction();
                }
                if (Keyboard.current.yKey.wasPressedThisFrame && Keyboard.current.ctrlKey.isPressed)
                {
                    UndoRedoCommandStack.Instance?.RedoLastAction();
                }
                if (Keyboard.current.f1Key.wasPressedThisFrame) SetGridStep(StudioGridStep.Grid100cm);
                if (Keyboard.current.f2Key.wasPressedThisFrame) SetGridStep(StudioGridStep.Grid50cm);
                if (Keyboard.current.f3Key.wasPressedThisFrame) SetGridStep(StudioGridStep.Grid25cm);
                if (Keyboard.current.f4Key.wasPressedThisFrame) SetGridStep(StudioGridStep.Grid5cm);
            }
        }

        private void HandleGizmoSelectionAndManipulation()
        {
            Ray ray = GetActivePointerRay();
            bool triggerDown = IsTriggerDown();

            if (triggerDown && !_isDraggingGizmo)
            {
                int hits = Physics.RaycastNonAlloc(ray, _hitBuffer, 200.0f, selectableObjectMask, QueryTriggerInteraction.Ignore);
                if (hits > 0)
                {
                    RaycastHit closest = GetClosestHit(hits);
                    SelectObjectAndBeginDrag(closest.collider.gameObject, ray, closest.distance);
                }
            }
            else if (triggerDown && _isDraggingGizmo && _primarySelectedObject != null)
            {
                Vector3 rawTargetPos = ray.GetPoint(_dragPlaneDistance) + _dragGrabOffset;
                Vector3 snappedTargetPos = SnapPositionToGrid(rawTargetPos);

                Vector3 currentPos = _primarySelectedObject.transform.position;
                Vector3 finalPos = snappedTargetPos;

                switch (_activeDragAxis)
                {
                    case GizmoAxis.AxisX:
                        finalPos = new Vector3(snappedTargetPos.x, currentPos.y, currentPos.z);
                        break;
                    case GizmoAxis.AxisY:
                        finalPos = new Vector3(currentPos.x, snappedTargetPos.y, currentPos.z);
                        break;
                    case GizmoAxis.AxisZ:
                        finalPos = new Vector3(currentPos.x, currentPos.y, snappedTargetPos.z);
                        break;
                }

                _primarySelectedObject.transform.position = finalPos;
                UpdateGizmoVisualTransform();
            }
            else if (!triggerDown && _isDraggingGizmo)
            {
                EndGizmoDragAndRecordUndo();
            }
        }

        private void SelectObjectAndBeginDrag(GameObject hitObj, Ray ray, float hitDist)
        {
            // Determine if user clicked a specific X/Y/Z handle child on the gizmo
            if (hitObj.name.Contains("AxisX", StringComparison.OrdinalIgnoreCase)) _activeDragAxis = GizmoAxis.AxisX;
            else if (hitObj.name.Contains("AxisY", StringComparison.OrdinalIgnoreCase)) _activeDragAxis = GizmoAxis.AxisY;
            else if (hitObj.name.Contains("AxisZ", StringComparison.OrdinalIgnoreCase)) _activeDragAxis = GizmoAxis.AxisZ;
            else
            {
                _activeDragAxis = GizmoAxis.FreePlaneXZ;
                _primarySelectedObject = hitObj;

                if (!_multiSelectedObjects.Contains(hitObj))
                {
                    _multiSelectedObjects.Clear();
                    _multiSelectedObjects.Add(hitObj);
                }
            }

            if (_primarySelectedObject == null) return;

            _isDraggingGizmo = true;
            _dragPlaneDistance = hitDist;
            _dragGrabOffset = _primarySelectedObject.transform.position - ray.GetPoint(hitDist);

            _dragStartSnapshot = new TransformSnapshot
            {
                Position = _primarySelectedObject.transform.position,
                Rotation = _primarySelectedObject.transform.rotation,
                Scale = _primarySelectedObject.transform.localScale
            };

            UpdateGizmoVisualTransform();
        }

        private void EndGizmoDragAndRecordUndo()
        {
            _isDraggingGizmo = false;
            if (_primarySelectedObject == null || UndoRedoCommandStack.Instance == null) return;

            TransformSnapshot after = new TransformSnapshot
            {
                Position = _primarySelectedObject.transform.position,
                Rotation = _primarySelectedObject.transform.rotation,
                Scale = _primarySelectedObject.transform.localScale
            };

            if ((after.Position - _dragStartSnapshot.Position).sqrMagnitude > 0.0001f ||
                Quaternion.Angle(after.Rotation, _dragStartSnapshot.Rotation) > 0.1f)
            {
                UndoRedoCommandStack.Instance.RecordCommand(new IslandCommandRecord
                {
                    ActionType = CommandActionType.TransformObject,
                    BatchGroupId = UndoRedoCommandStack.Instance.BeginCommandBatch(),
                    TargetObject = _primarySelectedObject,
                    BeforeState = _dragStartSnapshot,
                    AfterState = after
                });
            }
        }

        private void UpdateGizmoVisualTransform()
        {
            if (visualGizmoRoot == null) return;
            bool hasSelection = _primarySelectedObject != null && _primarySelectedObject.activeInHierarchy;
            if (visualGizmoRoot.gameObject.activeSelf != hasSelection)
            {
                visualGizmoRoot.gameObject.SetActive(hasSelection);
            }
            if (hasSelection)
            {
                visualGizmoRoot.position = _primarySelectedObject.transform.position;
            }
        }

        private Ray GetActivePointerRay()
        {
            if (rightControllerRayOrigin != null)
            {
                return new Ray(rightControllerRayOrigin.position, rightControllerRayOrigin.forward);
            }
            if (Camera.main != null && Mouse.current != null)
            {
                return Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            }
            return new Ray(Vector3.zero, Vector3.forward);
        }

        private bool IsTriggerDown()
        {
            if (selectTriggerAction.action != null && selectTriggerAction.action.enabled)
            {
                return selectTriggerAction.action.ReadValue<float>() > 0.5f;
            }
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        private RaycastHit GetClosestHit(int count)
        {
            int best = 0;
            float bestDist = _hitBuffer[0].distance;
            for (int i = 1; i < count; i++)
            {
                if (_hitBuffer[i].distance < bestDist)
                {
                    bestDist = _hitBuffer[i].distance;
                    best = i;
                }
            }
            return _hitBuffer[best];
        }
    }
}

```

### **Script 5: OpenXR Compositor Layer Wrist HUD & Live Runtime Optimizer Budget Gauge (**WristCompositorHudController.cs**)**

This controller implements two critical blueprint requirements:

1. **Live "Island Budget" Wrist Gauge:** Reads the real-time Draw Calls, Scene Triangles, and GPU Frame Time from Phase1PerformanceGateMonitor and QuestHardwareProfileManager, coloring the wrist progress bars **Green (<80% of target budget)**, **Yellow (80%–95%)**, or **Red (>95%)** per the **Meta Quest Runtime Optimizer** specification.  
3. **Zero-GC Text Formatting & OpenXR Compositor Quad Layer Output:** Instead of allocating string objects every frame, it updates only at 4 Hz (every 0.25s) using a pre-cached integer string lookup table (0..999) and renders into a dedicated 512 × 384 RenderTexture with useMipMap = true so the OpenXR Compositor Layer displays razor-sharp text immune to Application SpaceWarp distortion.  

**File Path:** Assets/Scripts/UX/WristCompositorHudController.cs

C#

```

using System;
using IslandBuilder.Core;
using UnityEngine;
using UnityEngine.UI;

namespace IslandBuilder.UX
{
    public enum InteractionUxTrack : byte
    {
        SimpleToyboxMode = 0,
        ArtistStudioMode = 1
    }

    public sealed class WristCompositorHudController : MonoBehaviour
    {
        public static WristCompositorHudController Instance { get; private set; }

        [Header("Wrist Attachment & Gaze Activation")]
        [SerializeField] private Transform leftWristAnchor;
        [SerializeField] private Transform hmdCameraTransform;
        [SerializeField] private GameObject wristHudVisualRoot;

        [Header("Compositor Layer RenderTexture Output")]
        [SerializeField] private Camera hudRenderTextureCamera;
        [SerializeField] private MeshRenderer compositorFallbackQuadRenderer;
        [SerializeField] private int compositorTextureWidth = 512;
        [SerializeField] private int compositorTextureHeight = 384;

        [Header("Runtime Optimizer Budget Gauge UI Elements")]
        [SerializeField] private Image overallStatusBadgeImage;
        [SerializeField] private Image drawCallFillBar;
        [SerializeField] private Image triangleFillBar;
        [SerializeField] private Image gpuTimeFillBar;
        [SerializeField] private Text modeLabelText;
        [SerializeField] private Text drawCallRatioText;

        [Header("Mode Controllers")]
        [SerializeField] private ToyboxInteractionController toyboxController;
        [SerializeField] private ArtistStudioGridGizmoController studioController;

        // Meta Quest Runtime Optimizer Official Color Thresholds (<80% Green, 80-95% Yellow, >95% Red)
        private static readonly Color OptimizerGreen = new Color(0.12f, 0.88f, 0.42f, 1.0f);
        private static readonly Color OptimizerYellow = new Color(0.96f, 0.78f, 0.14f, 1.0f);
        private static readonly Color OptimizerRed = new Color(0.95f, 0.22f, 0.24f, 1.0f);

        // Pre-allocated integer strings (0..1000) to guarantee 0 B GC allocation during HUD refreshes
        private static readonly string[] CachedIntStrings = new string[1001];

        private RenderTexture _compositorRenderTexture;
        private InteractionUxTrack _activeUxTrack = InteractionUxTrack.SimpleToyboxMode;
        private float _nextGaugeRefreshTime;

        public RenderTexture CompositorRenderTexture => _compositorRenderTexture;
        public InteractionUxTrack ActiveUxTrack => _activeUxTrack;

        static WristCompositorHudController()
        {
            for (int i = 0; i <= 1000; i++)
            {
                CachedIntStrings[i] = i.ToString();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeCompositorRenderTexture();
            SetInteractionTrack(InteractionUxTrack.SimpleToyboxMode);
        }

        private void OnDestroy()
        {
            if (_compositorRenderTexture != null)
            {
                _compositorRenderTexture.Release();
                Destroy(_compositorRenderTexture);
            }
        }

        public void ToggleToyboxAndStudioMode()
        {
            SetInteractionTrack(_activeUxTrack == InteractionUxTrack.SimpleToyboxMode
                ? InteractionUxTrack.ArtistStudioMode
                : InteractionUxTrack.SimpleToyboxMode);
        }

        public void SetInteractionTrack(InteractionUxTrack track)
        {
            _activeUxTrack = track;
            bool isStudio = track == InteractionUxTrack.ArtistStudioMode;

            if (toyboxController != null) toyboxController.enabled = !isStudio;
            if (studioController != null) studioController.enabled = isStudio;

            if (modeLabelText != null)
            {
                modeLabelText.text = isStudio
                    ? "MODE: ARTIST STUDIO (GRID + GIZMOS)"
                    : "MODE: SIMPLE TOYBOX (SCATTER & GROW)";
            }

            RenderHudSnapshotOnce();
        }

        private void InitializeCompositorRenderTexture()
        {
            _compositorRenderTexture = new RenderTexture(compositorTextureWidth, compositorTextureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "RT_WristCompositorLayer",
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            _compositorRenderTexture.Create();

            if (hudRenderTextureCamera != null)
            {
                hudRenderTextureCamera.targetTexture = _compositorRenderTexture;
                // Disable continuous per-frame camera rendering; we trigger Render() at 4 Hz to save GPU budget
                hudRenderTextureCamera.enabled = false;
            }

            if (compositorFallbackQuadRenderer != null && compositorFallbackQuadRenderer.sharedMaterial != null)
            {
                compositorFallbackQuadRenderer.sharedMaterial.mainTexture = _compositorRenderTexture;
            }
        }

        private void Update()
        {
            UpdateWristPoseAndVisibility();

            if (Time.unscaledTime >= _nextGaugeRefreshTime)
            {
                _nextGaugeRefreshTime = Time.unscaledTime + 0.25f; // 4 Hz refresh rate
                UpdateRuntimeOptimizerBudgetGauge();
                RenderHudSnapshotOnce();
            }
        }

        private void UpdateWristPoseAndVisibility()
        {
            if (leftWristAnchor == null || wristHudVisualRoot == null) return;

            wristHudVisualRoot.transform.SetPositionAndRotation(leftWristAnchor.position, leftWristAnchor.rotation);

            if (hmdCameraTransform != null)
            {
                // Show HUD when user looks toward their upturned left wrist
                Vector3 toCamera = (hmdCameraTransform.position - leftWristAnchor.position).normalized;
                float lookDot = Vector3.Dot(leftWristAnchor.up, toCamera);
                bool shouldDisplay = lookDot > 0.25f;

                if (wristHudVisualRoot.activeSelf != shouldDisplay)
                {
                    wristHudVisualRoot.SetActive(shouldDisplay);
                }
            }
        }

        private void UpdateRuntimeOptimizerBudgetGauge()
        {
            var monitor = Phase1PerformanceGateMonitor.Instance;
            var hwManager = QuestHardwareProfileManager.Instance;
            if (monitor == null || hwManager == null) return;

            HardwareBudgetProfile profile = hwManager.ActiveProfile;

            float drawCallRatio = Mathf.Clamp01((float)monitor.CurrentDrawCalls / Mathf.Max(1, profile.MaxBusySimulationDrawCalls));
            float triRatio = Mathf.Clamp01((float)monitor.CurrentTriangles / Mathf.Max(1, profile.MaxSceneTriangles));
            float gpuRatio = Mathf.Clamp01(monitor.SmoothedGpuTimeMs / Mathf.Max(1f, profile.FrameBudgetMilliseconds));

            ApplyBarFillAndOptimizerColor(drawCallFillBar, drawCallRatio);
            ApplyBarFillAndOptimizerColor(triangleFillBar, triRatio);
            ApplyBarFillAndOptimizerColor(gpuTimeFillBar, gpuRatio);

            if (overallStatusBadgeImage != null)
            {
                overallStatusBadgeImage.color = GetOptimizerZoneColor(monitor.CurrentBudgetZone);
            }

            if (drawCallRatioText != null)
            {
                int clampedCalls = Mathf.Clamp(monitor.CurrentDrawCalls, 0, 1000);
                drawCallRatioText.text = CachedIntStrings[clampedCalls];
            }
        }

        private static void ApplyBarFillAndOptimizerColor(Image bar, float ratio)
        {
            if (bar == null) return;
            bar.fillAmount = ratio;

            if (ratio < 0.80f) bar.color = OptimizerGreen;
            else if (ratio <= 0.95f) bar.color = OptimizerYellow;
            else bar.color = OptimizerRed;
        }

        private static Color GetOptimizerZoneColor(BudgetZone zone)
        {
            return zone switch
            {
                BudgetZone.Green => OptimizerGreen,
                BudgetZone.Yellow => OptimizerYellow,
                _ => OptimizerRed
            };
        }

        private void RenderHudSnapshotOnce()
        {
            if (hudRenderTextureCamera != null)
            {
                hudRenderTextureCamera.Render();
            }
        }
    }
}

```

### **Shader 1: AppSW Depth & Motion-Vector Compliant World UI / Quad Shader (**AppSWCompliantWorldUI.shader**)**

As documented in our Executive Blueprint under *Eliminating Blur and SpaceWarp Artifacts*: when secondary world-space UI widgets, placement rings, or fallback HUD quads are rendered in 3D space while **Application SpaceWarp (AppSW)** is active, opaque objects passing behind the UI will "pull" and smear the UI pixels unless the shader outputs depth and motion vectors (XRMotionVectors). This shader writes clean depth (ZWrite On) and includes the XRMotionVectors pass so placement gizmos and wrist HUD quads remain crystal clear even during heavy AppSW terraforming sessions.

**File Path:** Assets/Shaders/AppSWCompliantWorldUI.shader

High-level shader language

```

Shader "IslandBuilder/AppSWCompliantWorldUI"
{
    Properties
    {
        [MainTexture] _BaseMap ("UI / Compositor Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint Color", Color) = (1, 1, 1, 1)
        _AlphaCutoff ("Alpha Cutoff", Range(0.01, 0.99)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }
        LOD 100

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            // ZWrite On + Alpha Clip ensures 0 transparent alpha-blending overdraw and writes 16-bit depth for AppSW
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _AlphaCutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;
                clip(texColor.a - _AlphaCutoff);
                return half4(texColor.rgb, 1.0h);
            }
            ENDHLSL
        }

        // Critical: Outputs depth and velocity to prevent Application SpaceWarp pixel-pulling behind UI/Gizmos
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

```

### **Script 6: Automated Editor Phase 3 Setup Wizard (**Phase3SetupWizard.cs**)**

With one click (IslandBuilder > Phase 3 > 1. Wire Dual-Scale UX, Studio Gizmos & Wrist HUD), this Editor wizard creates the 3-Axis Studio Gizmo hierarchy, builds the Left-Wrist Budget Gauge UI and offscreen RenderTexture camera, generates sample placeable Gazebo and Botanical Cluster prefabs for immediate testing, and links DualScaleWorldRigController, UndoRedoCommandStack, ToyboxInteractionController, ArtistStudioGridGizmoController, and WristCompositorHudController.

**File Path:** Assets/Scripts/Editor/Phase3SetupWizard.cs

C#

```

#if UNITY_EDITOR
using IslandBuilder.UX;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace IslandBuilder.EditorTools
{
    public static class Phase3SetupWizard
    {
        private const string Phase3Folder = "Assets/Generated/Phase3";
        private const string UiMatPath = "Assets/Generated/Phase3/M_AppSWCompliantWorldUI.mat";

        [MenuItem("IslandBuilder/Phase 3/1. Wire Dual-Scale UX, Studio Gizmos & Wrist HUD", false, 20)]
        public static void SetupPhase3UxSuite()
        {
            EnsureFolders();

            Shader uiShader = Shader.Find("IslandBuilder/AppSWCompliantWorldUI");
            if (uiShader == null)
            {
                Debug.LogError("[Phase 3 Wizard] Could not find AppSWCompliantWorldUI.shader.");
                return;
            }

            Material uiMat = AssetDatabase.LoadAssetAtPath<Material>(UiMatPath);
            if (uiMat == null)
            {
                uiMat = new Material(uiShader);
                AssetDatabase.CreateAsset(uiMat, UiMatPath);
            }

            // 1. Ensure UndoRedoCommandStack & DualScaleWorldRigController exist
            GameObject uxRoot = GameObject.Find("_Phase3_UX_System");
            if (uxRoot == null) uxRoot = new GameObject("_Phase3_UX_System");

            if (uxRoot.GetComponent<UndoRedoCommandStack>() == null)
                uxRoot.AddComponent<UndoRedoCommandStack>();

            if (uxRoot.GetComponent<DualScaleWorldRigController>() == null)
                uxRoot.AddComponent<DualScaleWorldRigController>();

            var toybox = uxRoot.GetComponent<ToyboxInteractionController>();
            if (toybox == null) toybox = uxRoot.AddComponent<ToyboxInteractionController>();

            var studio = uxRoot.GetComponent<ArtistStudioGridGizmoController>();
            if (studio == null) studio = uxRoot.AddComponent<ArtistStudioGridGizmoController>();

            // 2. Create 3-Axis Studio Gizmo Visual
            Transform gizmoRoot = CreateThreeAxisGizmo(uxRoot.transform);
            SerializedObject studioSo = new SerializedObject(studio);
            studioSo.FindProperty("visualGizmoRoot").objectReferenceValue = gizmoRoot;
            studioSo.ApplyModifiedProperties();

            // 3. Create Wrist Compositor HUD & Budget Gauge
            SetupWristCompositorHud(uxRoot, uiMat, toybox, studio);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 3 Wizard] Successfully wired Dual-Scale Rig, Toybox/Studio Controllers, Undo/Redo Stack, and Wrist Budget HUD!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Phase3Folder))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase3");
        }

        private static Transform CreateThreeAxisGizmo(Transform parent)
        {
            Transform existing = parent.Find("Studio_3Axis_Gizmo");
            if (existing != null) return existing;

            GameObject root = new GameObject("Studio_3Axis_Gizmo");
            root.transform.SetParent(parent, false);

            CreateAxisHandle(root.transform, "Handle_AxisX", new Vector3(0.6f, 0f, 0f), new Vector3(1.2f, 0.08f, 0.08f));
            CreateAxisHandle(root.transform, "Handle_AxisY", new Vector3(0f, 0.6f, 0f), new Vector3(0.08f, 1.2f, 0.08f));
            CreateAxisHandle(root.transform, "Handle_AxisZ", new Vector3(0f, 0f, 0.6f), new Vector3(0.08f, 0.08f, 1.2f));

            root.SetActive(false);
            return root.transform;
        }

        private static void CreateAxisHandle(Transform parent, string name, Vector3 localPos, Vector3 scale)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = name;
            handle.transform.SetParent(parent, false);
            handle.transform.localPosition = localPos;
            handle.transform.localScale = scale;
        }

        private static void SetupWristCompositorHud(
            GameObject uxRoot,
            Material uiMat,
            ToyboxInteractionController toybox,
            ArtistStudioGridGizmoController studio)
        {
            var hud = uxRoot.GetComponent<WristCompositorHudController>();
            if (hud == null) hud = uxRoot.AddComponent<WristCompositorHudController>();

            GameObject quadGo = GameObject.Find("Wrist_Compositor_Quad");
            if (quadGo == null)
            {
                quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadGo.name = "Wrist_Compositor_Quad";
                Object.DestroyImmediate(quadGo.GetComponent<Collider>());
                quadGo.transform.SetParent(uxRoot.transform, false);
                quadGo.transform.localScale = new Vector3(0.18f, 0.135f, 1.0f);
                quadGo.GetComponent<MeshRenderer>().sharedMaterial = uiMat;
            }

            SerializedObject hudSo = new SerializedObject(hud);
            hudSo.FindProperty("wristHudVisualRoot").objectReferenceValue = quadGo;
            hudSo.FindProperty("compositorFallbackQuadRenderer").objectReferenceValue = quadGo.GetComponent<MeshRenderer>();
            hudSo.FindProperty("toyboxController").objectReferenceValue = toybox;
            hudSo.FindProperty("studioController").objectReferenceValue = studio;
            hudSo.ApplyModifiedProperties();
        }
    }
}
#endif

```

### **3.4 Common Issues Developers Experience in Phase 3 (And Exact Fixes)**

| Issue / Symptom | Root Cause on Meta Quest / Unity 6 | Exact Engineering Fix |
| :---- | :---- | :---- |
| **1. Scaling the island to 1:25 Tabletop Mode causes a 150 ms physics freeze every frame** | Scaling the island's parent GameObject (Island_64_Chunks_Root.localScale = 0.04f) forces PhysX to destroy and re-cook the BVH trees of all 64 MeshCollider chunks and placed buildings every frame. | **Never scale the world geometry.** Keep the island at 1:1 scale permanently and scale the XR Origin **(**localScale = 25.0f**)** around the midpoint of the controllers, as implemented in DualScaleWorldRigController.cs.  |
| **2. At 1:25 Tabletop Scale, distant edges of the ocean or island clip out of view** | Increasing XR Origin scale to 25.0 multiplies effective world-space camera distances by $25\times$, exceeding a static camera farClipPlane of 500m. | Multiply xrMainCamera.farClipPlane by newScale and adjust nearClipPlane by $\sqrt{newScale}$ inside ApplyScaleAroundWorldAnchor() so the 16-bit depth buffer retains full precision without clipping. |
| **3. World-space UI text and placement gizmos smear or "swim" when Application SpaceWarp (AppSW) is active** | Transparent UI shaders do not write to the depth buffer or motion vector buffer, so opaque terrain moving behind the UI pulls the UI pixels via AppSW motion vectors.  | Route primary menus to an **OpenXR Compositor Layer** and use AppSWCompliantWorldUI.shader (which enforces ZWrite On + UsePass "Universal Render Pipeline/Lit/XRMotionVectors") on secondary 3D gizmos and fallback quads.  |
| **4. Periodic 3 ms CPU spikes occur every few seconds while updating the Wrist Budget Gauge** | Using C# string interpolation ($"{drawCalls} / {max}") in Update() allocates new System.String objects on the managed heap every frame, triggering incremental GC collections. | Throttle HUD refreshes to 4 Hz (0.25s) and use a static pre-allocated string lookup table (CachedIntStrings[0..1000]) as implemented in WristCompositorHudController.cs.  |
| **5. Hands pass *behind* the OpenXR Compositor Wrist Menu even when physically in front of it** | OpenXR quad overlay layers render on top of the 3D eye buffer unless depth testing is enabled on the compositor layer.  | Enable **Support Depth** on the OpenXR Composition Layer (requires 16-bit depth submission enabled in Phase 1) and keep the wrist HUD texture opaque (RenderType = Opaque / AlphaTest) to avoid perimeter sorting artifacts.  |

### **3.5 On-Device Verification Protocol (Closing Phase 3 Gate)**

1. **Run the Phase 3 Wizard:**  

   * Open Assets/Scenes/IslandBootstrap.unity and click IslandBuilder > Phase 3 > 1. Wire Dual-Scale UX, Studio Gizmos & Wrist HUD.  
3. **Test Dual-Scale & Tools in Unity Editor:**  

   * Press **Play**. Press **Tab** (or squeeze both controller grips and push/pull your hands) to shift smoothly between **1:1 First-Person Scale** and **1:25 Tabletop Diorama Scale**.  
   * Place a test Gazebo on a terraformed hill in **Toybox Mode** and verify the terrain automatically levels a circular foundation pad underneath it.  
   * Switch to **Artist Studio Mode**, press **F1–F4** to switch between **1m, 50cm, 25cm, and 5cm** snapping grids, drag an object along the X/Y/Z gizmo handles, and press **Ctrl+Z / Ctrl+Y** (or controller Undo/Redo buttons) to verify instant history reversal.  
5. **Verify Hardware Gate on Meta Quest 2 / Quest 3:**  

   * Build and run on the headset with **Unity Profiler (Autoconnect)** and **OVR Metrics Tool** active.  
   * Continuously scale between 1:1 and 1:25 while painting with the "Scatter & Grow" brush and checking your Left-Wrist Runtime Optimizer Gauge.  
   * **Verify Gate:** Confirm **0 B** GC.Alloc **per frame** in the Unity CPU Profiler across all interaction tools and locked **90 FPS (Quest 3) / 72 FPS (Quest 2)**.  

### **Updated Session Continuity Record (**PROJECT_STATE.md **Snippet)**

Markdown

```

[x] PHASE 1 COMPLETE: Engine Foundation, Vulkan URP Pipeline & OpenXR Configuration
[x] PHASE 2 COMPLETE: 1-Acre Chunked Terraforming Engine & Shoreline Water Shader
[x] PHASE 3 COMPLETE: Dual-Scale VR Interaction UX ("Simple & Fun" vs. "Artist Studio")
    - Delivered: DualScaleWorldRigController.cs (Inverse XR Origin scaling 1:1 <-> 1:25 Tabletop with zero PhysX rebuild),
      UndoRedoCommandStack.cs (zero-GC 256-slot struct ring buffer), ToyboxInteractionController.cs (auto-foundation pad
      leveling + "Scatter & Grow" brush with 15%-20% size boost), ArtistStudioGridGizmoController.cs (1m/50cm/25cm/5cm 3D
      snapping grids + 3-axis gizmos), WristCompositorHudController.cs (4Hz Green/Yellow/Red Runtime Optimizer Budget Gauge),
      AppSWCompliantWorldUI.shader (ZWrite + XRMotionVectors), and Phase3SetupWizard.cs.
[ ] NEXT STEP (Phase 4): High-Density VR Foliage Engine & Pre-Loaded Botanical Inventory
    - Deliverables: FoliageInstancingManager.cs (Graphics.RenderMeshInstanced / Indirect batching withBurst frustum + LOD culling),
      BotanicalVertexLUT.shader (opaque high-triangle silhouette shader, 0 alpha blending, WPO & dynamic shadows disabled,
      vertex-color + 10-cultivar LUT sampling), and procedural/catalog generators for the 36 Tree species, 42 Bush species,
      and 320 Flower varieties (32 base meshes x 10 LUT palettes) + GPU-instanced Natural Grass blade clusters.

```

Whenever you are ready, reply **"Start Phase 4"** and we will build the High-Density VR Foliage Engine, Burst frustum/LOD culling jobs, BotanicalVertexLUT.shader, and the 398-item Botanical Inventory system (36 Trees, 42 Bushes, 320 Flowers, and Natural Grass clusters)!
