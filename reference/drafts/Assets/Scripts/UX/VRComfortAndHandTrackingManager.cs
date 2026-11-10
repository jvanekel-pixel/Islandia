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
