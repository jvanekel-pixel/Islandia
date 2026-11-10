using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace IslandBuilder.Core
{
    public enum QuestHardwareTier
    {
        UnknownEditor = 0,
        Quest2OrPro = 1,
        Quest3Or3S = 2
    }

    [Serializable]
    public struct HardwareBudgetProfile
    {
        public QuestHardwareTier Tier;
        public float TargetRefreshRate;
        public float FrameBudgetMilliseconds;
        public float Phase1GpuCeilingMilliseconds;
        public float RenderViewportScale;
        public float FoveatedRenderingLevel;
        public int MaxBaselineDrawCalls;
        public int MaxBusySimulationDrawCalls;
        public int MaxSceneTriangles;
    }

    [DefaultExecutionOrder(-500)]
    public sealed class QuestHardwareProfileManager : MonoBehaviour
    {
        public static QuestHardwareProfileManager Instance { get; private set; }

        [Header("Runtime Detected Profile")]
        [SerializeField] private HardwareBudgetProfile activeProfile;

        private XRDisplaySubsystem _displaySubsystem;
        private bool _isSubsystemInitialized;

        public HardwareBudgetProfile ActiveProfile => activeProfile;
        public XRDisplaySubsystem DisplaySubsystem => _displaySubsystem;
        public bool IsSubsystemInitialized => _isSubsystemInitialized;

        public event Action<HardwareBudgetProfile> OnHardwareProfileConfigured;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            DetectHardwareTierAndBudgets();
        }

        private IEnumerator Start()
        {
            // CRITICAL UNITY 6 REQUIREMENT:
            // XRDisplaySubsystem takes up to 3 frames to fully initialize.
            // Setting foveatedRenderingLevel or scaleOfAllRenderTargets on frame 0 is ignored by OpenXR.
            yield return null;
            yield return null;
            yield return null;

            InitializeXRDisplaySubsystem();
        }

        private void DetectHardwareTierAndBudgets()
        {
            string deviceModel = SystemInfo.deviceModel ?? string.Empty;
            string gpuName = SystemInfo.graphicsDeviceName ?? string.Empty;

            // Quest 3 and Quest 3S use Snapdragon XR2 Gen 2 (Adreno 740)
            // Quest 2 uses Snapdragon XR2 Gen 1 (Adreno 650)
            bool isQuest3Generation = gpuName.Contains("740", StringComparison.OrdinalIgnoreCase) ||
                                      deviceModel.Contains("Quest 3", StringComparison.OrdinalIgnoreCase) ||
                                      deviceModel.Contains("Eureka", StringComparison.OrdinalIgnoreCase) ||
                                      deviceModel.Contains("Panther", StringComparison.OrdinalIgnoreCase);

            if (isQuest3Generation)
            {
                activeProfile = new HardwareBudgetProfile
                {
                    Tier = QuestHardwareTier.Quest3Or3S,
                    TargetRefreshRate = 90.0f,
                    FrameBudgetMilliseconds = 11.1f,
                    Phase1GpuCeilingMilliseconds = 6.0f,
                    RenderViewportScale = 1.0f,
                    FoveatedRenderingLevel = 0.75f, // Frees 15%-25% GPU headroom
                    MaxBaselineDrawCalls = 200,
                    MaxBusySimulationDrawCalls = 300,
                    MaxSceneTriangles = 1500000
                };
            }
            else
            {
                // Default to strict Quest 2 baseline for safety on older devices
                activeProfile = new HardwareBudgetProfile
                {
                    Tier = Application.isEditor ? QuestHardwareTier.UnknownEditor : QuestHardwareTier.Quest2OrPro,
                    TargetRefreshRate = 72.0f,
                    FrameBudgetMilliseconds = 13.8f,
                    Phase1GpuCeilingMilliseconds = 6.0f,
                    RenderViewportScale = 0.92f,
                    FoveatedRenderingLevel = 1.0f, // Max peripheral foveation on Quest 2
                    MaxBaselineDrawCalls = 100,
                    MaxBusySimulationDrawCalls = 200,
                    MaxSceneTriangles = 750000
                };
            }

            // Lock Unity Physics & Target Frame Rate to match refresh rate
            Application.targetFrameRate = Mathf.RoundToInt(activeProfile.TargetRefreshRate);
            Time.fixedDeltaTime = 1.0f / activeProfile.TargetRefreshRate;
            QualitySettings.vSyncCount = 1;
        }

        private void InitializeXRDisplaySubsystem()
        {
            var displaySubsystems = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displaySubsystems);

            if (displaySubsystems.Count == 0)
            {
                Debug.LogWarning("[QuestHardwareProfileManager] No active XRDisplaySubsystem found (normal in desktop Editor without XR Simulator).");
                OnHardwareProfileConfigured?.Invoke(activeProfile);
                return;
            }

            _displaySubsystem = displaySubsystems[0];
            _isSubsystemInitialized = true;

            // 1. Configure SRP Foveated Rendering
            FoveatedRenderingCaps caps = SystemInfo.foveatedRenderingCaps;
            if (caps != FoveatedRenderingCaps.None)
            {
                _displaySubsystem.foveatedRenderingLevel = activeProfile.FoveatedRenderingLevel;
                _displaySubsystem.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.GazeAllowed;
                Debug.Log($"[QuestHardwareProfileManager] Foveated Rendering enabled at level {activeProfile.FoveatedRenderingLevel:F2} (Caps: {caps}).");
            }
            else
            {
                Debug.LogWarning("[QuestHardwareProfileManager] Device reports FoveatedRenderingCaps.None. Verify 'Foveated Rendering' is checked in OpenXR settings.");
            }

            // 2. Apply Viewport Scaling
            _displaySubsystem.scaleOfAllViewports = activeProfile.RenderViewportScale;

            // 3. Request Target Refresh Rate via Meta OpenXR extension if available
            RequestTargetDisplayRefreshRate(activeProfile.TargetRefreshRate);

            OnHardwareProfileConfigured?.Invoke(activeProfile);
        }

        public void SetDynamicViewportScale(float scale)
        {
            if (!_isSubsystemInitialized || _displaySubsystem == null) return;
            _displaySubsystem.scaleOfAllViewports = Mathf.Clamp(scale, 0.7f, 1.2f);
        }

        private void RequestTargetDisplayRefreshRate(float targetHz)
        {
            // Uses reflection to invoke Meta OpenXR display refresh rate extension if present,
            // avoiding hard compile failures if package versions change.
            if (_displaySubsystem == null) return;

            var method = _displaySubsystem.GetType().GetMethod("TryRequestDisplayRefreshRate",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(_displaySubsystem, new object[] { targetHz });
                Debug.Log($"[QuestHardwareProfileManager] Requested Display Refresh Rate: {targetHz} Hz.");
            }
        }
    }
}
