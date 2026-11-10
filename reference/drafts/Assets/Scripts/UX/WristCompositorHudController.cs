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
