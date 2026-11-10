using IslandBuilder.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IslandBuilder.TerrainSystem
{
    public sealed class VRTerraformingBrushController : MonoBehaviour
    {
        [Header("Ray & Visual Ring References")]
        [SerializeField] private Transform rightControllerRayOrigin;
        [SerializeField] private Transform brushPreviewRingTransform;
        [SerializeField] private LayerMask terrainLayerMask = ~0;

        [Header("Brush Settings")]
        [SerializeField] private TerraformingBrushMode activeBrushMode = TerraformingBrushMode.Raise;
        [SerializeField] private TerrainSurfaceLayer activePaintLayer = TerrainSurfaceLayer.GrassOrTurf;
        [SerializeField, Range(0.75f, 8.0f)] private float brushRadiusMeters = 2.5f;
        [SerializeField, Range(0.5f, 10.0f)] private float brushStrength = 3.5f;
        [SerializeField] private bool autoPaintSurfaceWhileSculpting = true;

        [Header("XR Input Actions (Optional - Falls Back to XR Trigger / Mouse)")]
        [SerializeField] private InputActionProperty triggerValueAction;
        [SerializeField] private InputActionProperty thumbstickAdjustAction;

        private bool _wasSculptingLastFrame;
        private float _lockedFlattenElevation;

        public TerraformingBrushMode ActiveBrushMode
        {
            get => activeBrushMode;
            set => activeBrushMode = value;
        }

        public TerrainSurfaceLayer ActivePaintLayer
        {
            get => activePaintLayer;
            set => activePaintLayer = value;
        }

        private void Update()
        {
            if (IslandTerrainManager.Instance == null) return;

            HandleRadiusAndModeShortcuts();

            if (TryGetBrushRayHit(out RaycastHit hit))
            {
                UpdateBrushPreviewRing(true, hit.point, hit.normal);

                float triggerIntensity = ReadTriggerIntensity();
                bool isSculptingNow = triggerIntensity > 0.15f;

                if (isSculptingNow && !_wasSculptingLastFrame)
                {
                    // Lock flatten elevation at the start of the stroke
                    _lockedFlattenElevation = IslandTerrainManager.Instance.SampleHeightAtWorldPosition(hit.point);

                    // Activate Application SpaceWarp during active sculpting per Blueprint Spec
                    if (DynamicSpaceWarpController.Instance != null)
                    {
                        DynamicSpaceWarpController.Instance.AcquireSpaceWarpLock();
                    }
                }

                if (isSculptingNow)
                {
                    IslandTerrainManager.Instance.ApplyBrushStroke(
                        hit.point,
                        brushRadiusMeters,
                        brushStrength * triggerIntensity,
                        activeBrushMode,
                        activePaintLayer,
                        _lockedFlattenElevation,
                        autoPaintSurfaceWhileSculpting);
                }
                else if (_wasSculptingLastFrame)
                {
                    EndSculptStroke();
                }

                _wasSculptingLastFrame = isSculptingNow;
            }
            else
            {
                UpdateBrushPreviewRing(false, Vector3.zero, Vector3.up);
                if (_wasSculptingLastFrame)
                {
                    EndSculptStroke();
                    _wasSculptingLastFrame = false;
                }
            }
        }

        private void EndSculptStroke()
        {
            IslandTerrainManager.Instance.FlushDeferredColliderBaking();
            if (DynamicSpaceWarpController.Instance != null)
            {
                DynamicSpaceWarpController.Instance.ReleaseSpaceWarpLock();
            }
        }

        private bool TryGetBrushRayHit(out RaycastHit hit)
        {
            if (rightControllerRayOrigin != null)
            {
                Ray vrRay = new Ray(rightControllerRayOrigin.position, rightControllerRayOrigin.forward);
                return Physics.Raycast(vrRay, out hit, 80.0f, terrainLayerMask, QueryTriggerInteraction.Ignore);
            }

            if (Camera.main != null && Mouse.current != null)
            {
                Ray mouseRay = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                return Physics.Raycast(mouseRay, out hit, 120.0f, terrainLayerMask, QueryTriggerInteraction.Ignore);
            }

            hit = default;
            return false;
        }

        private float ReadTriggerIntensity()
        {
            if (triggerValueAction.action != null && triggerValueAction.action.enabled)
            {
                return triggerValueAction.action.ReadValue<float>();
            }

            // Editor Mouse Fallback: Left Click = Active Mode, Right Click = Lower/Excavate Pond
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.isPressed) return 1.0f;
                if (Mouse.current.rightButton.isPressed)
                {
                    activeBrushMode = TerraformingBrushMode.Lower;
                    return 1.0f;
                }
            }

            return 0.0f;
        }

        private void HandleRadiusAndModeShortcuts()
        {
            if (thumbstickAdjustAction.action != null && thumbstickAdjustAction.action.enabled)
            {
                Vector2 stick = thumbstickAdjustAction.action.ReadValue<Vector2>();
                if (Mathf.Abs(stick.x) > 0.25f)
                {
                    brushRadiusMeters = Mathf.Clamp(brushRadiusMeters + stick.x * 2.5f * Time.deltaTime, 0.75f, 8.0f);
                }
            }

            // Editor Keyboard Quick-Testing Shortcuts (1-5 = Modes, G = Toggle Astroturf/Grass)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) activeBrushMode = TerraformingBrushMode.Raise;
                if (Keyboard.current.digit2Key.wasPressedThisFrame) activeBrushMode = TerraformingBrushMode.Lower;
                if (Keyboard.current.digit3Key.wasPressedThisFrame) activeBrushMode = TerraformingBrushMode.Flatten;
                if (Keyboard.current.digit4Key.wasPressedThisFrame) activeBrushMode = TerraformingBrushMode.Smooth;
                if (Keyboard.current.digit5Key.wasPressedThisFrame) activeBrushMode = TerraformingBrushMode.PaintSurface;
                if (Keyboard.current.gKey.wasPressedThisFrame)
                {
                    IslandTerrainManager.Instance.UseAstroturfMode = !IslandTerrainManager.Instance.UseAstroturfMode;
                }
            }
        }

        private void UpdateBrushPreviewRing(bool visible, Vector3 worldPos, Vector3 worldNormal)
        {
            if (brushPreviewRingTransform == null) return;
            if (brushPreviewRingTransform.gameObject.activeSelf != visible)
            {
                brushPreviewRingTransform.gameObject.SetActive(visible);
            }

            if (visible)
            {
                brushPreviewRingTransform.position = worldPos + worldNormal * 0.03f;
                brushPreviewRingTransform.rotation = Quaternion.FromToRotation(Vector3.up, worldNormal);
                brushPreviewRingTransform.localScale = new Vector3(brushRadiusMeters * 2f, 1f, brushRadiusMeters * 2f);
            }
        }
    }
}
