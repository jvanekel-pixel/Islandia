using System;
using System.Collections.Generic;
using IslandBuilder.Core;
using UnityEngine;

namespace IslandBuilder.Media
{
    public sealed class VirtualCinemaManager : MonoBehaviour
    {
        public static VirtualCinemaManager Instance { get; private set; }

        [Header("1080p OpenXR Compositor Layer Shared RenderTexture")]
        [SerializeField] private int primaryCompositorWidth = 1920;
        [SerializeField] private int primaryCompositorHeight = 1080;
        [SerializeField] private int secondaryFallbackWidth = 1280;
        [SerializeField] private int secondaryFallbackHeight = 720;

        [Header("Cinema Dimmer (Theater Evening Mode)")]
        [SerializeField] private bool autoDimIslandWhenProjectorPlays;
        [SerializeField, Range(0f, 1f)] private float theaterNightTimeOfDay = 0.04f;
        [SerializeField, Range(0f, 1f)] private float normalDayTimeOfDay = 0.38f;

        private readonly List<VirtualCinemaScreenController> _activeScreensLru =
            new List<VirtualCinemaScreenController>(4);

        private RenderTexture _primary1080pCompositorTexture;
        private RenderTexture _secondary720pFallbackTexture;
        private VirtualCinemaScreenController _currentPrimaryCompositorScreen;
        private bool _isTheaterDimmed;

        public RenderTexture Primary1080pCompositorTexture => _primary1080pCompositorTexture;
        public RenderTexture Secondary720pFallbackTexture => _secondary720pFallbackTexture;
        public VirtualCinemaScreenController CurrentPrimaryCompositorScreen => _currentPrimaryCompositorScreen;
        public int ActiveScreenCount => _activeScreensLru.Count;

        /// <summary>
        /// P2 #14 Remediation: Strict Hard Cap on simultaneously decoding video screens per hardware tier.
        /// Quest 2 / Quest Pro = 1 Active Screen Max | Quest 3 / Quest 3S = 2 Active Screens Max.
        /// </summary>
        public int MaxAllowedActiveScreens
        {
            get
            {
                if (IslandRemediationLightingAndSaveManager.Instance == null) return 1;
                var tier = IslandRemediationLightingAndSaveManager.Instance.DetectedTier;
                return (tier == RemediatedHardwareTier.Quest3 || tier == RemediatedHardwareTier.Quest3S || tier == RemediatedHardwareTier.DesktopEditor)
                    ? 2
                    : 1;
            }
        }

        public event Action<VirtualCinemaScreenController> OnPrimaryCompositorScreenChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            AllocateSharedVideoRenderTextures();
        }

        private void OnDestroy()
        {
            if (_primary1080pCompositorTexture != null)
            {
                _primary1080pCompositorTexture.Release();
                Destroy(_primary1080pCompositorTexture);
            }
            if (_secondary720pFallbackTexture != null)
            {
                _secondary720pFallbackTexture.Release();
                Destroy(_secondary720pFallbackTexture);
            }
        }

        private void AllocateSharedVideoRenderTextures()
        {
            // Pre-allocate fixed 1080p and 720p RenderTextures once at startup so turning TVs on/off causes 0 VRAM fragmentation
            _primary1080pCompositorTexture = new RenderTexture(primaryCompositorWidth, primaryCompositorHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = "RT_Cinema_Primary_1080p_Compositor",
                useMipMap = false, // Compositor layer samples 1:1 without mipmap blur
                filterMode = FilterMode.Bilinear
            };
            _primary1080pCompositorTexture.Create();

            _secondary720pFallbackTexture = new RenderTexture(secondaryFallbackWidth, secondaryFallbackHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = "RT_Cinema_Secondary_720p_World",
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear
            };
            _secondary720pFallbackTexture.Create();
        }

        /// <summary>
        /// Registers a screen as playing and enforces the P2 #14 Hard Cap (evicting the oldest screen to standby if needed).
        /// Always promotes the newly activated screen to the Primary 1080p OpenXR Compositor Layer.
        /// </summary>
        public RenderTexture RequestScreenActivationAndAssignTexture(VirtualCinemaScreenController screen)
        {
            if (screen == null) return null;

            _activeScreensLru.Remove(screen);

            int maxCap = MaxAllowedActiveScreens;
            while (_activeScreensLru.Count >= maxCap)
            {
                // P2 #14: Evict Least-Recently-Used screen to zero-cost Standby Mode
                VirtualCinemaScreenController oldest = _activeScreensLru[0];
                _activeScreensLru.RemoveAt(0);
                if (oldest != null)
                {
                    oldest.ForceEnterStandbyFromCapEviction();
                }
            }

            _activeScreensLru.Add(screen);
            ReassignPrimaryAndSecondaryRenderTargets();

            if (autoDimIslandWhenProjectorPlays && screen.IsProjectorOrDriveIn)
            {
                SetTheaterDimmerMode(true);
            }

            return screen == _currentPrimaryCompositorScreen
                ? _primary1080pCompositorTexture
                : _secondary720pFallbackTexture;
        }

        public void NotifyScreenStopped(VirtualCinemaScreenController screen)
        {
            if (_activeScreensLru.Remove(screen))
            {
                ReassignPrimaryAndSecondaryRenderTargets();
            }

            if (_activeScreensLru.Count == 0 && _isTheaterDimmed)
            {
                SetTheaterDimmerMode(false);
            }
        }

        public void SetTheaterDimmerMode(bool dimForCinema)
        {
            _isTheaterDimmed = dimForCinema;
            if (IslandRemediationLightingAndSaveManager.Instance != null)
            {
                IslandRemediationLightingAndSaveManager.Instance.UpdateSunAndTrilightForTimeOfDay(
                    dimForCinema ? theaterNightTimeOfDay : normalDayTimeOfDay);
            }
        }

        private void ReassignPrimaryAndSecondaryRenderTargets()
        {
            if (_activeScreensLru.Count == 0)
            {
                _currentPrimaryCompositorScreen = null;
                OnPrimaryCompositorScreenChanged?.Invoke(null);
                return;
            }

            // Most recently interacted screen (last in list) gets the 1080p OpenXR Compositor Layer
            _currentPrimaryCompositorScreen = _activeScreensLru[_activeScreensLru.Count - 1];
            _currentPrimaryCompositorScreen.AssignTargetRenderTexture(_primary1080pCompositorTexture, isPrimaryCompositorLayer: true);

            // If on Quest 3/3S and a 2nd screen is active, assign it the 720p world quad target
            if (_activeScreensLru.Count > 1 && _activeScreensLru[0] != null)
            {
                _activeScreensLru[0].AssignTargetRenderTexture(_secondary720pFallbackTexture, isPrimaryCompositorLayer: false);
            }

            OnPrimaryCompositorScreenChanged?.Invoke(_currentPrimaryCompositorScreen);
        }
    }
}
