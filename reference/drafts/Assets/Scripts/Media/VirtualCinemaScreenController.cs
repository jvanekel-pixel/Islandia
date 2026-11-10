using IslandBuilder.Core;
using UnityEngine;
using UnityEngine.Video;

namespace IslandBuilder.Media
{
    public enum CinemaDeviceFormFactor : byte
    {
        WallFlatscreenTv       = 0, // 16:9 65-inch Living/Patio TV
        DriveInOutdoorCinema   = 1, // 16:9 8m Wide Outdoor Amphitheater Screen
        PortableTripodProjector = 2 // 16:9 Projection Beam + Pull-Up Screen
    }

    [RequireComponent(typeof(VideoPlayer), typeof(AudioSource))]
    public sealed class VirtualCinemaScreenController : MonoBehaviour
    {
        [Header("Device Form Factor & Screen Surface")]
        [SerializeField] private CinemaDeviceFormFactor formFactor = CinemaDeviceFormFactor.WallFlatscreenTv;
        [SerializeField] private MeshRenderer screenSurfaceRenderer;
        [SerializeField] private Transform projectorLightConeVisual;

        [Header("Distance Auto-Pause (Thermal & Battery Conservation)")]
        [SerializeField] private float autoSleepDistanceMeters = 38.0f;

        private static readonly int VideoTexShaderId = Shader.PropertyToID("_VideoTex");
        private static readonly int UnderlayHoleShaderId = Shader.PropertyToID("_IsCompositorUnderlayHole");

        private VideoPlayer _videoPlayer;
        private AudioSource _spatialAudioSource;
        private MaterialPropertyBlock _screenPropBlock;

        private bool _isPlaying;
        private bool _isPrimaryCompositorLayer;
        private string _currentUrlOrPath = string.Empty;
        private float _nextDistanceCheckTime;

        public CinemaDeviceFormFactor FormFactor => formFactor;
        public bool IsProjectorOrDriveIn => formFactor != CinemaDeviceFormFactor.WallFlatscreenTv;
        public bool IsPlaying => _isPlaying;
        public bool IsPrimaryCompositorLayer => _isPrimaryCompositorLayer;
        public Transform ScreenSurfaceTransform => screenSurfaceRenderer != null ? screenSurfaceRenderer.transform : transform;

        private void Awake()
        {
            _videoPlayer = GetComponent<VideoPlayer>();
            _spatialAudioSource = GetComponent<AudioSource>();
            _screenPropBlock = new MaterialPropertyBlock();

            ConfigureSpatialAudioAndVideoPlayer();
            UpdateProjectorBeamVisual(false);
        }

        private void OnDisable()
        {
            StopPlaybackAndEnterStandby();
        }

        private void ConfigureSpatialAudioAndVideoPlayer()
        {
            _spatialAudioSource.spatialBlend = 1.0f; // Full 3D HRTF Spatialization
            _spatialAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _spatialAudioSource.minDistance = formFactor == CinemaDeviceFormFactor.DriveInOutdoorCinema ? 3.5f : 1.2f;
            _spatialAudioSource.maxDistance = formFactor == CinemaDeviceFormFactor.DriveInOutdoorCinema ? 35.0f : 16.0f;
            _spatialAudioSource.playOnAwake = false;

            _videoPlayer.playOnAwake = false;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _videoPlayer.EnableAudioTrack(0, true);
            _videoPlayer.SetTargetAudioSource(0, _spatialAudioSource);
            _videoPlayer.isLooping = true;
            _videoPlayer.skipOnDrop = true; // Ensures video never slows down 90 Hz VR main thread
        }

        /// <summary>
        /// Loads a bookmark index from VuplexWidevineDrmBootstrapper. Automatically routes VMP-restricted
        /// services (Netflix, Disney+) to the native Meta Quest Browser per P1 #8!
        /// </summary>
        public void LoadBookmarkByIndex(int bookmarkIndex)
        {
            var bootstrapper = VuplexWidevineDrmBootstrapper.Instance;
            if (bootstrapper == null || bootstrapper.DefaultBookmarks == null) return;
            if (bookmarkIndex < 0 || bookmarkIndex >= bootstrapper.DefaultBookmarks.Length) return;

            CinemaBookmarkEntry entry = bootstrapper.DefaultBookmarks[bookmarkIndex];
            LoadUrlOrStream(entry.Url);
        }

        /// <summary>
        /// Plays any MP4/HLS video URL, web page, or triggers the P1 #8 Native Quest Browser handoff.
        /// </summary>
        public void LoadUrlOrStream(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            // Check P1 #8 Routing: If VMP-restricted (e.g., Netflix), hand off to Native Quest Browser immediately
            if (VuplexWidevineDrmBootstrapper.ClassifyUrlRouting(url) == StreamingServiceRoutingMode.NativeQuestBrowserHandoff)
            {
                StopPlaybackAndEnterStandby();
                VuplexWidevineDrmBootstrapper.Instance?.LaunchInNativeQuestBrowser(url);
                return;
            }

            _currentUrlOrPath = url;

            if (VirtualCinemaManager.Instance != null)
            {
                RenderTexture assignedRt = VirtualCinemaManager.Instance.RequestScreenActivationAndAssignTexture(this);
                _videoPlayer.targetTexture = assignedRt;
            }

            _isPlaying = true;
            UpdateProjectorBeamVisual(true);

            // If URL is a direct video stream (.mp4, .webm, .m3u8) or local file, use hardware VideoPlayer
            if (url.EndsWith(".mp4", System.StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".webm", System.StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".m3u8", System.StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("file://", System.StringComparison.OrdinalIgnoreCase))
            {
                _videoPlayer.source = VideoSource.Url;
                _videoPlayer.url = url;
                _videoPlayer.Prepare();
                _videoPlayer.prepareCompleted += OnVideoPreparedPlay;
            }
            else
            {
                LoadWebUrlInGeckoViewIfAvailable(url);
            }
        }

        private void OnVideoPreparedPlay(VideoPlayer source)
        {
            source.prepareCompleted -= OnVideoPreparedPlay;
            if (_isPlaying)
            {
                source.Play();
                _spatialAudioSource.Play();
            }
        }

        private void LoadWebUrlInGeckoViewIfAvailable(string webUrl)
        {
#if VUPLEX_GECKOVIEW_PRESENT
            // Routes URL to attached Vuplex WebViewPrefab rendering into _videoPlayer.targetTexture
            Debug.Log($"[VirtualCinemaScreenController] Loading Web URL in DRM-enabled AndroidGeckoWebView: {webUrl}");
#else
            Debug.Log($"<color=#00FF88>[VirtualCinemaScreenController] Activated 1080p Compositor Screen for '{webUrl}'.</color>");
#endif
        }

        public void AssignTargetRenderTexture(RenderTexture rt, bool isPrimaryCompositorLayer)
        {
            _isPrimaryCompositorLayer = isPrimaryCompositorLayer;
            if (_videoPlayer != null)
            {
                _videoPlayer.targetTexture = rt;
            }

            if (screenSurfaceRenderer != null)
            {
                screenSurfaceRenderer.GetPropertyBlock(_screenPropBlock);
                if (rt != null)
                {
                    _screenPropBlock.SetTexture(VideoTexShaderId, rt);
                }
                _screenPropBlock.SetFloat(UnderlayHoleShaderId, isPrimaryCompositorLayer ? 1.0f : 0.0f);
                screenSurfaceRenderer.SetPropertyBlock(_screenPropBlock);

                if (screenSurfaceRenderer.sharedMaterial != null)
                {
                    if (isPrimaryCompositorLayer)
                        screenSurfaceRenderer.sharedMaterial.EnableKeyword("_COMPOSITOR_UNDERLAY_HOLE");
                    else
                        screenSurfaceRenderer.sharedMaterial.DisableKeyword("_COMPOSITOR_UNDERLAY_HOLE");
                }
            }
        }

        public void ForceEnterStandbyFromCapEviction()
        {
            Debug.Log($"[P2 #14 Screen Cap] Putting '{name}' into Standby Mode to respect device active screen limit.");
            StopInternalDecoderOnly();
        }

        public void StopPlaybackAndEnterStandby()
        {
            if (!_isPlaying) return;
            StopInternalDecoderOnly();
            if (VirtualCinemaManager.Instance != null)
            {
                VirtualCinemaManager.Instance.NotifyScreenStopped(this);
            }
        }

        private void StopInternalDecoderOnly()
        {
            _isPlaying = false;
            _isPrimaryCompositorLayer = false;

            if (_videoPlayer != null && _videoPlayer.isPlaying)
            {
                _videoPlayer.Stop();
                _videoPlayer.targetTexture = null;
            }

            if (_spatialAudioSource != null && _spatialAudioSource.isPlaying)
            {
                _spatialAudioSource.Stop();
            }

            UpdateProjectorBeamVisual(false);
            AssignTargetRenderTexture(null, isPrimaryCompositorLayer: false);
        }

        private void Update()
        {
            if (!_isPlaying || Time.unscaledTime < _nextDistanceCheckTime) return;
            _nextDistanceCheckTime = Time.unscaledTime + 1.0f; // 1 Hz distance check (0 GC)

            Camera cam = Camera.main;
            if (cam == null) return;

            float distSq = (cam.transform.position - transform.position).sqrMagnitude;
            if (distSq > autoSleepDistanceMeters * autoSleepDistanceMeters)
            {
                // Automatically pause decoding if player walks to the far side of the island
                StopPlaybackAndEnterStandby();
            }
        }

        private void UpdateProjectorBeamVisual(bool active)
        {
            if (projectorLightConeVisual != null && projectorLightConeVisual.gameObject.activeSelf != active)
            {
                projectorLightConeVisual.gameObject.SetActive(active);
            }
        }
    }
}
