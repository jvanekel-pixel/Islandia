using Unity.Profiling;
using UnityEngine;
using UnityEngine.XR;

namespace IslandBuilder.Core
{
    public enum BudgetZone
    {
        Green = 0,  // < 80% of target budget
        Yellow = 1, // 80% - 95% of target budget
        Red = 2     // > 95% of target budget
    }

    public sealed class Phase1PerformanceGateMonitor : MonoBehaviour
    {
        public static Phase1PerformanceGateMonitor Instance { get; private set; }

        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];

        private ProfilerRecorder _setPassCallsRecorder;
        private ProfilerRecorder _drawCallsRecorder;
        private ProfilerRecorder _trianglesRecorder;

        private float _smoothedGpuTimeMs;
        private float _smoothedCpuTimeMs;
        private int _currentDrawCalls;
        private int _currentTriangles;
        private BudgetZone _currentBudgetZone;
        private bool _isPhase1GatePassing = true;

        public float SmoothedGpuTimeMs => _smoothedGpuTimeMs;
        public float SmoothedCpuTimeMs => _smoothedCpuTimeMs;
        public int CurrentDrawCalls => _currentDrawCalls;
        public int CurrentTriangles => _currentTriangles;
        public BudgetZone CurrentBudgetZone => _currentBudgetZone;
        public bool IsPhase1GatePassing => _isPhase1GatePassing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            _setPassCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        private void OnDisable()
        {
            _setPassCallsRecorder.Dispose();
            _drawCallsRecorder.Dispose();
            _trianglesRecorder.Dispose();
        }

        private void Update()
        {
            SampleHardwareMetrics();
            EvaluateBudgetThresholds();
        }

        private void SampleHardwareMetrics()
        {
            float rawGpuMs = 0f;
            float rawCpuMs = Time.unscaledDeltaTime * 1000.0f;

            var hwManager = QuestHardwareProfileManager.Instance;
            if (hwManager != null && hwManager.IsSubsystemInitialized && hwManager.DisplaySubsystem != null)
            {
                if (hwManager.DisplaySubsystem.TryGetAppGPUTimeLastFrame(out float xrGpuSeconds))
                {
                    rawGpuMs = xrGpuSeconds * 1000.0f;
                }
            }

            if (rawGpuMs <= 0.001f)
            {
                FrameTimingManager.CaptureFrameTimings();
                uint count = FrameTimingManager.GetLatestTimings(1, _frameTimings);
                if (count > 0)
                {
                    rawGpuMs = (float)_frameTimings[0].gpuFrameTime;
                    rawCpuMs = (float)_frameTimings[0].cpuFrameTime;
                }
            }

            // Exponential moving average to prevent single-frame jitter
            _smoothedGpuTimeMs = Mathf.Lerp(_smoothedGpuTimeMs, rawGpuMs, 0.1f);
            _smoothedCpuTimeMs = Mathf.Lerp(_smoothedCpuTimeMs, rawCpuMs, 0.1f);

            if (_drawCallsRecorder.Valid) _currentDrawCalls = (int)_drawCallsRecorder.LastValue;
            if (_trianglesRecorder.Valid) _currentTriangles = (int)_trianglesRecorder.LastValue;
        }

        private void EvaluateBudgetThresholds()
        {
            var hwManager = QuestHardwareProfileManager.Instance;
            if (hwManager == null) return;

            var profile = hwManager.ActiveProfile;

            // Phase 1 Specific Gate: Empty ocean + flat 1-acre island must stay < 6.0 ms GPU
            _isPhase1GatePassing = _smoothedGpuTimeMs <= profile.Phase1GpuCeilingMilliseconds;

            float gpuRatio = _smoothedGpuTimeMs / profile.FrameBudgetMilliseconds;
            float drawCallRatio = (float)_currentDrawCalls / profile.MaxBaselineDrawCalls;
            float worstRatio = Mathf.Max(gpuRatio, drawCallRatio);

            if (worstRatio < 0.80f)
            {
                _currentBudgetZone = BudgetZone.Green;
            }
            else if (worstRatio <= 0.95f)
            {
                _currentBudgetZone = BudgetZone.Yellow;
            }
            else
            {
                _currentBudgetZone = BudgetZone.Red;
                if (DynamicSpaceWarpController.Instance != null)
                {
                    DynamicSpaceWarpController.Instance.NotifyGpuSpikeDetected();
                }
            }
        }
    }
}
