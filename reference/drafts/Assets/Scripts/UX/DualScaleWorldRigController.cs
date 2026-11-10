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
