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
