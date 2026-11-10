using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace IslandBuilder.Core
{
    public sealed class DynamicSpaceWarpController : MonoBehaviour
    {
        public static DynamicSpaceWarpController Instance { get; private set; }

        [Header("References")]
        [Tooltip("Assign the XR Origin Transform (Tracking Space root), NOT the CenterEyeCamera.")]
        [SerializeField] private Transform xrOriginTransform;

        [Header("Automatic Frame-Spike Trigger")]
        [SerializeField] private bool enableAutoTriggerOnGpuSpike = true;
        [Tooltip("Hysteresis duration in seconds to keep AppSW active after a spike subsides.")]
        [SerializeField] private float holdDurationSeconds = 2.0f;

        private bool _isSpaceWarpActive;
        private int _manualRequestRefCount;
        private float _autoHoldTimer;

#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport("UnityOpenXR", EntryPoint = "MetaSetSpaceWarp")]
        private static extern void NativeMetaSetSpaceWarp(bool enabled);

        [DllImport("UnityOpenXR", EntryPoint = "MetaSetAppSpacePosition")]
        private static extern void NativeMetaSetAppSpacePosition(float x, float y, float z);

        [DllImport("UnityOpenXR", EntryPoint = "MetaSetAppSpaceRotation")]
        private static extern void NativeMetaSetAppSpaceRotation(float x, float y, float z, float w);
#endif

        public bool IsSpaceWarpActive => _isSpaceWarpActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void LateUpdate()
        {
            if (_autoHoldTimer > 0f)
            {
                _autoHoldTimer -= Time.unscaledDeltaTime;
                if (_autoHoldTimer <= 0f && _manualRequestRefCount == 0)
                {
                    ApplySpaceWarpState(false);
                }
            }

            // Meta OpenXR SpaceWarp requires updating the AppSpace (XR Origin) position
            // and rotation every frame while SpaceWarp is active so synthesized frames
            // distinguish player locomotion from object motion.
            if (_isSpaceWarpActive && xrOriginTransform != null)
            {
                UpdateAppSpaceTransform(xrOriginTransform.position, xrOriginTransform.rotation);
            }
        }

        /// <summary>
        /// Call when starting heavy operations (e.g., Phase 2 Terraforming Brush Down or Phase 7 glTF batch spawn).
        /// </summary>
        public void AcquireSpaceWarpLock()
        {
            _manualRequestRefCount++;
            if (!_isSpaceWarpActive)
            {
                ApplySpaceWarpState(true);
            }
        }

        /// <summary>
        /// Call when finishing heavy operations (e.g., Terraforming Brush Up).
        /// </summary>
        public void ReleaseSpaceWarpLock()
        {
            _manualRequestRefCount = Mathf.Max(0, _manualRequestRefCount - 1);
            if (_manualRequestRefCount == 0 && _autoHoldTimer <= 0f && _isSpaceWarpActive)
            {
                ApplySpaceWarpState(false);
            }
        }

        /// <summary>
        /// Called by Phase1PerformanceGateMonitor if GPU frame time exceeds 90% of hardware budget.
        /// </summary>
        public void NotifyGpuSpikeDetected()
        {
            if (!enableAutoTriggerOnGpuSpike) return;
            _autoHoldTimer = holdDurationSeconds;
            if (!_isSpaceWarpActive)
            {
                ApplySpaceWarpState(true);
            }
        }

        private void ApplySpaceWarpState(bool enable)
        {
            _isSpaceWarpActive = enable;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                NativeMetaSetSpaceWarp(enable);
            }
            catch (EntryPointNotFoundException)
            {
                Debug.LogWarning("[DynamicSpaceWarpController] UnityOpenXR MetaSetSpaceWarp entry point not found. Ensure Meta XR SpaceWarp feature is enabled in OpenXR settings.");
            }
#endif
        }

        private static void UpdateAppSpaceTransform(Vector3 pos, Quaternion rot)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                NativeMetaSetAppSpacePosition(pos.x, pos.y, pos.z);
                NativeMetaSetAppSpaceRotation(rot.x, rot.y, rot.z, rot.w);
            }
            catch (EntryPointNotFoundException)
            {
                // Ignored if native entry point unavailable
            }
#endif
        }
    }
}
