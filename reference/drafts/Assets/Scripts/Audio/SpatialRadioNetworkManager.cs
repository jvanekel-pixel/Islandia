using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace IslandBuilder.Audio
{
    public enum OptionalSpotifyMode : byte
    {
        DisabledUsePrimaryRadio = 0, // Default Launch Configuration (P1 #7 Compliant)
        ModeB_WebApiRemoteConnect = 1, // Controls Spotify app on user's phone/PC via Web API
        ModeA_GeckoWebPlaybackSdk = 2  // Personal / Dev-Mode embedded Web Playback SDK
    }

    public sealed class SpatialRadioNetworkManager : MonoBehaviour
    {
        public static SpatialRadioNetworkManager Instance { get; private set; }

        [Header("Primary Radio State")]
        [SerializeField] private int currentStationIndex;
        [SerializeField, Range(0f, 1f)] private float masterRadioVolume = 0.78f;
        [SerializeField] private bool isRadioPoweredOn = true;

        [Header("P2 #18 Ambient Island Soundscape Budget (< 0.8% CPU)")]
        [SerializeField] private bool enableOceanAndBreezeAmbience = true;
        [SerializeField, Range(0f, 0.5f)] private float ambientSoundscapeVolume = 0.18f;

        [Header("P1 #7 Optional Personal / Dev-Mode Spotify Settings (Off by Default)")]
        [SerializeField] private OptionalSpotifyMode optionalSpotifyMode = OptionalSpotifyMode.DisabledUsePrimaryRadio;
        [Tooltip("Personal OAuth Bearer Token for Dev-Mode testing (Max 25 allowlisted users per Spotify May 2025 policy).")]
        [SerializeField] private string personalDevSpotifyBearerToken = string.Empty;

        private readonly List<SpatialBoxRadioController> _registeredRadios = new List<SpatialBoxRadioController>(8);
        private AudioClip _currentSharedClip;
        private AudioSource _ambientOceanSource;

        // Zero-GC 64-sample buffer for computing live speaker RMS at 30 Hz (avoids expensive FFT spectrum calls!)
        private readonly float[] _rmsSampleScratch = new float[64];
        private float _currentSmoothedRmsLevel;
        private float _nextRmsSampleTime;

        public int CurrentStationIndex => currentStationIndex;
        public float MasterRadioVolume => masterRadioVolume;
        public bool IsRadioPoweredOn => isRadioPoweredOn;
        public float CurrentSmoothedRmsLevel => _currentSmoothedRmsLevel;
        public float NormalizedStationDialPosition
        {
            get
            {
                var cat = PrimaryAudioCatalogAndScanner.Instance;
                if (cat == null || cat.AllAvailableStations.Count <= 1) return 0.2f;
                return (float)currentStationIndex / (cat.AllAvailableStations.Count - 1);
            }
        }

        /// <summary>
        /// Fired whenever the station, volume, or power state changes (also read by Phase 11 Pet Harmony AI
        /// so pets gather around active radios to sunbathe!).
        /// </summary>
        public event Action<RadioStationDescriptor, bool> OnRadioStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (enableOceanAndBreezeAmbience)
            {
                InitializeProceduralOceanAmbienceBed();
            }
        }

        private async void Start()
        {
            if (isRadioPoweredOn)
            {
                await TuneToStationIndexAsync(currentStationIndex);
            }
        }

        public void RegisterRadioSpeaker(SpatialBoxRadioController radio)
        {
            if (radio == null || _registeredRadios.Contains(radio)) return;
            _registeredRadios.Add(radio);
            SyncSpeakerToSharedStream(radio);
        }

        public void UnregisterRadioSpeaker(SpatialBoxRadioController radio)
        {
            _registeredRadios.Remove(radio);
        }

        /// <summary>
        /// Returns the closest active Box Radio in world space (used by Phase 11 Pet Harmony AI so pets can
        /// walk over and sunbathe beside the music).
        /// </summary>
        public bool TryGetClosestActiveRadioPosition(Vector3 queryWorldPos, out Vector3 radioWorldPos)
        {
            radioWorldPos = Vector3.zero;
            if (!isRadioPoweredOn || _registeredRadios.Count == 0) return false;

            float bestDistSq = float.MaxValue;
            bool found = false;

            for (int i = 0; i < _registeredRadios.Count; i++)
            {
                var r = _registeredRadios[i];
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                float dSq = (r.transform.position - queryWorldPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    radioWorldPos = r.transform.position;
                    found = true;
                }
            }
            return found;
        }

        public void SetMasterVolume(float volume01)
        {
            masterRadioVolume = Mathf.Clamp01(volume01);
            for (int i = 0; i < _registeredRadios.Count; i++)
            {
                if (_registeredRadios[i] != null)
                {
                    _registeredRadios[i].ApplyNetworkVolume(masterRadioVolume);
                }
            }
        }

        public async void ToggleRadioPower()
        {
            isRadioPoweredOn = !isRadioPoweredOn;
            if (isRadioPoweredOn)
            {
                await TuneToStationIndexAsync(currentStationIndex);
            }
            else
            {
                for (int i = 0; i < _registeredRadios.Count; i++)
                {
                    _registeredRadios[i]?.StopSpeakerPlayback();
                }

                if (optionalSpotifyMode == OptionalSpotifyMode.ModeB_WebApiRemoteConnect)
                {
                    _ = SendOptionalSpotifyConnectCommandAsync("pause");
                }
            }
        }

        public async void CycleNextStation()
        {
            var cat = PrimaryAudioCatalogAndScanner.Instance;
            if (cat == null || cat.AllAvailableStations.Count == 0) return;
            int next = (currentStationIndex + 1) % cat.AllAvailableStations.Count;
            await TuneToStationIndexAsync(next);
        }

        public async Task TuneToStationIndexAsync(int index)
        {
            var cat = PrimaryAudioCatalogAndScanner.Instance;
            if (cat == null || cat.AllAvailableStations.Count == 0) return;

            currentStationIndex = Mathf.Clamp(index, 0, cat.AllAvailableStations.Count - 1);
            RadioStationDescriptor station = cat.AllAvailableStations[currentStationIndex];

            _currentSharedClip = await cat.OpenStreamingAudioClipAsync(station);
            isRadioPoweredOn = true;

            for (int i = 0; i < _registeredRadios.Count; i++)
            {
                SyncSpeakerToSharedStream(_registeredRadios[i]);
            }

            if (optionalSpotifyMode == OptionalSpotifyMode.ModeB_WebApiRemoteConnect &&
                !string.IsNullOrEmpty(personalDevSpotifyBearerToken))
            {
                _ = SendOptionalSpotifyConnectCommandAsync("play");
            }

            Debug.Log($"<color=#00FF88>[SpatialRadioNetwork] Tuned to [{station.StationName}] ({station.GenreSubtitle})</color>");
            OnRadioStateChanged?.Invoke(station, isRadioPoweredOn);
        }

        private void SyncSpeakerToSharedStream(SpatialBoxRadioController radio)
        {
            if (radio == null || _currentSharedClip == null || !isRadioPoweredOn) return;

            // Synchronize sample offset with the primary speaker so multiple radios on the island play in phase!
            int referenceSample = 0;
            if (_registeredRadios.Count > 0 && _registeredRadios[0] != null && _registeredRadios[0] != radio)
            {
                referenceSample = _registeredRadios[0].CurrentPlaybackSamplePosition;
            }

            radio.StartSpeakerPlayback(_currentSharedClip, masterRadioVolume, referenceSample);
        }

        private void Update()
        {
            // Sample 64-float RMS envelope at 30 Hz (every 0.033s) for VU meters -> 0 B GC, < 0.02 ms CPU!
            if (Time.unscaledTime < _nextRmsSampleTime) return;
            _nextRmsSampleTime = Time.unscaledTime + 0.033f;

            if (!isRadioPoweredOn || _registeredRadios.Count == 0 || _registeredRadios[0] == null)
            {
                _currentSmoothedRmsLevel = Mathf.Lerp(_currentSmoothedRmsLevel, 0f, 0.25f);
                return;
            }

            AudioSource primarySource = _registeredRadios[0].SpeakerAudioSource;
            if (primarySource != null && primarySource.isPlaying)
            {
                primarySource.GetOutputData(_rmsSampleScratch, 0);
                float sumSq = 0f;
                for (int i = 0; i < _rmsSampleScratch.Length; i++)
                {
                    float s = _rmsSampleScratch[i];
                    sumSq += s * s;
                }
                float rms = Mathf.Sqrt(sumSq / _rmsSampleScratch.Length);
                float normalizedVu = Mathf.Clamp01(rms * 4.5f);
                _currentSmoothedRmsLevel = Mathf.Lerp(_currentSmoothedRmsLevel, normalizedVu, 0.35f);
            }
        }

        /// <summary>
        /// P1 #7 Optional Personal / Dev-Mode Spotify Web API Remote Control (PUT/POST https://api.spotify.com/v1/me/player/...).
        /// Only invoked if the developer explicitly enables OptionalSpotifyMode and supplies a personal token.
        /// </summary>
        public async Task<bool> SendOptionalSpotifyConnectCommandAsync(string endpointAction)
        {
            if (optionalSpotifyMode != OptionalSpotifyMode.ModeB_WebApiRemoteConnect ||
                string.IsNullOrWhiteSpace(personalDevSpotifyBearerToken))
            {
                return false;
            }

            string method = (endpointAction == "next" || endpointAction == "previous")
                ? UnityWebRequest.kHttpVerbPOST
                : UnityWebRequest.kHttpVerbPUT;

            string url = $"https://api.spotify.com/v1/me/player/{endpointAction}";

            using (var req = new UnityWebRequest(url, method))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", $"Bearer {personalDevSpotifyBearerToken}");
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 8;

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    await Task.Yield();
                }

                return req.result == UnityWebRequest.Result.Success;
            }
        }

        private void InitializeProceduralOceanAmbienceBed()
        {
            GameObject ambGo = new GameObject("Island_Ocean_Breeze_Ambience_Bed");
            ambGo.transform.SetParent(transform, false);
            _ambientOceanSource = ambGo.AddComponent<AudioSource>();
            _ambientOceanSource.spatialBlend = 0.0f; // Lightweight 2D bed (< 0.2% CPU)
            _ambientOceanSource.loop = true;
            _ambientOceanSource.volume = ambientSoundscapeVolume;

            // Generate 6-second pink-noise ocean wave wash loop
            const int rate = 22050;
            const int seconds = 6;
            int total = rate * seconds;
            float[] waveSamples = new float[total];
            float b0 = 0f, b1 = 0f, b2 = 0f;

            UnityEngine.Random.InitState(101);
            for (int i = 0; i < total; i++)
            {
                float white = UnityEngine.Random.Range(-1f, 1f);
                b0 = 0.997f * b0 + white * 0.029f;
                b1 = 0.985f * b1 + white * 0.032f;
                b2 = 0.950f * b2 + white * 0.048f;
                float pink = (b0 + b1 + b2) * 0.15f;

                // Slow 6-second ocean wave crest & recede envelope
                float waveEnv = 0.35f + 0.65f * Mathf.Sin(((float)i / total) * Mathf.PI * 2f) * Mathf.Sin(((float)i / total) * Mathf.PI);
                waveSamples[i] = pink * waveEnv;
            }

            AudioClip oceanClip = AudioClip.Create("Procedural_Ocean_Surf_Loop", total, 1, rate, stream: false);
            oceanClip.SetData(waveSamples, 0);
            _ambientOceanSource.clip = oceanClip;
            _ambientOceanSource.Play();
        }
    }
}
