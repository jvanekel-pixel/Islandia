using System;
using UnityEngine;

namespace IslandBuilder.Media
{
    public enum StreamingServiceRoutingMode : byte
    {
        InWorldCinemaScreen = 0, // YouTube, Vimeo, Twitch, Plex, Jellyfin, Local MP4, Widevine Test
        NativeQuestBrowserHandoff = 1 // P1 #8: Netflix, Disney+, Prime Video, Apple TV+ (VMP-restricted)
    }

    [Serializable]
    public struct CinemaBookmarkEntry
    {
        public string Title;
        public string Url;
        public StreamingServiceRoutingMode RoutingMode;
    }

    [DefaultExecutionOrder(-600)]
    public sealed class VuplexWidevineDrmBootstrapper : MonoBehaviour
    {
        public static VuplexWidevineDrmBootstrapper Instance { get; private set; }

        // Official Bitmovin / Shaka Widevine EME DRM Stream Test URLs per Vuplex Support Spec
        public const string WidevineDrmTestPageUrl = "https://bitmovin.com/demos/drm";

        private static readonly string[] VmpRestrictedDomains =
        {
            "netflix.com",
            "disneyplus.com",
            "primevideo.com",
            "max.com",
            "hulu.com",
            "tv.apple.com",
            "peacocktv.com"
        };

        [Header("Pre-Loaded Cinema Bookmarks (P1 #8 Compliant)")]
        [SerializeField] private CinemaBookmarkEntry[] defaultBookmarks =
        {
            new CinemaBookmarkEntry
            {
                Title = "YouTube Leanback Cinema",
                Url = "https://www.youtube.com",
                RoutingMode = StreamingServiceRoutingMode.InWorldCinemaScreen
            },
            new CinemaBookmarkEntry
            {
                Title = "Vimeo Staff Picks (1080p)",
                Url = "https://vimeo.com/channels/staffpicks",
                RoutingMode = StreamingServiceRoutingMode.InWorldCinemaScreen
            },
            new CinemaBookmarkEntry
            {
                Title = "Plex Web Media Server",
                Url = "https://app.plex.tv/desktop",
                RoutingMode = StreamingServiceRoutingMode.InWorldCinemaScreen
            },
            new CinemaBookmarkEntry
            {
                Title = "Widevine DRM Stream Validator",
                Url = WidevineDrmTestPageUrl,
                RoutingMode = StreamingServiceRoutingMode.InWorldCinemaScreen
            },
            new CinemaBookmarkEntry
            {
                Title = "Netflix (Open in Quest Browser - P1 #8)",
                Url = "https://www.netflix.com",
                RoutingMode = StreamingServiceRoutingMode.NativeQuestBrowserHandoff
            },
            new CinemaBookmarkEntry
            {
                Title = "Disney+ (Open in Quest Browser - P1 #8)",
                Url = "https://www.disneyplus.com",
                RoutingMode = StreamingServiceRoutingMode.NativeQuestBrowserHandoff
            }
        };

        public CinemaBookmarkEntry[] DefaultBookmarks => defaultBookmarks;

        public event Action<string> OnNativeQuestBrowserHandoffTriggered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnableAndroidGeckoWidevineDrm();
        }

        private static void EnableAndroidGeckoWidevineDrm()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && VUPLEX_GECKOVIEW_PRESENT
            // MANDATORY PHASE 8 CALL: Must execute in Awake() before any WebViewPrefab is initialized.
            Vuplex.WebView.AndroidGeckoWebView.SetDrmEnabled(true);
            Vuplex.WebView.Web.SetAutoplayEnabled(true);
            Debug.Log("<color=#00FF88>[VuplexWidevineDrmBootstrapper] AndroidGeckoWebView.SetDrmEnabled(true) initialized on Meta Quest.</color>");
#else
            Debug.Log("[VuplexWidevineDrmBootstrapper] Running in Native VideoPlayer / Editor mode (Add VUPLEX_GECKOVIEW_PRESENT define when Vuplex GeckoView is imported).");
#endif
        }

        /// <summary>
        /// P1 #8 Remediation: Determines whether a URL should play directly inside the in-world Virtual TV
        /// or hand off cleanly to the native Meta Quest Browser.
        /// </summary>
        public static StreamingServiceRoutingMode ClassifyUrlRouting(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return StreamingServiceRoutingMode.InWorldCinemaScreen;

            for (int i = 0; i < VmpRestrictedDomains.Length; i++)
            {
                if (url.IndexOf(VmpRestrictedDomains[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return StreamingServiceRoutingMode.NativeQuestBrowserHandoff;
                }
            }

            return StreamingServiceRoutingMode.InWorldCinemaScreen;
        }

        /// <summary>
        /// P1 #8 Remediation: Launches VMP-restricted streaming services directly in the native Meta Quest
        /// Horizon OS Browser window with one click, avoiding fragile VMP workarounds.
        /// </summary>
        public void LaunchInNativeQuestBrowser(string targetUrl)
        {
            if (string.IsNullOrWhiteSpace(targetUrl)) return;
            Debug.Log($"<color=#00FF88>[P1 #8 Quest Browser Handoff] Opening '{targetUrl}' in native Meta Quest Browser.</color>");
            OnNativeQuestBrowserHandoffTriggered?.Invoke(targetUrl);
            Application.OpenURL(targetUrl);
        }
    }
}
