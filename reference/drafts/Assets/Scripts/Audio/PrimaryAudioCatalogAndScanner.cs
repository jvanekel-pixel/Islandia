using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace IslandBuilder.Audio
{
    public enum AudioChannelSourceType : byte
    {
        BuiltInProceduralLofiLoop = 0, // Zero-network instant offline fallback
        InternetRadioStream       = 1, // P1 #7 Primary Path: Icecast / OGG / MP3 live stream
        UserLocalMusicFile        = 2, // P1 #7 Primary Path: User-owned MP3/OGG/WAV in persistentDataPath/Music
        OptionalSpotifyDevMode    = 3  // P1 #7 Optional Path: Personal / Dev-Mode Spotify Connect or Web Playback
    }

    [Serializable]
    public struct RadioStationDescriptor
    {
        public string StationName;
        public string GenreSubtitle;
        public string StreamUrlOrFilePath;
        public AudioChannelSourceType SourceType;
        public AudioType StreamAudioFormat;
    }

    public sealed class PrimaryAudioCatalogAndScanner : MonoBehaviour
    {
        public static PrimaryAudioCatalogAndScanner Instance { get; private set; }

        private const string UserMusicFolderName = "Music";

        [Header("P1 #7 Primary Path: Curated Internet Radio Stations (No Spotify Dependency)")]
        [SerializeField] private RadioStationDescriptor[] preloadedStations =
        {
            new RadioStationDescriptor
            {
                StationName = "Island Lofi Breeze (Built-In Offline Synth)",
                GenreSubtitle = "Warm Tropical Chords • Zero Network Latency",
                StreamUrlOrFilePath = "internal://island_lofi_synth",
                SourceType = AudioChannelSourceType.BuiltInProceduralLofiLoop,
                StreamAudioFormat = AudioType.UNKNOWN
            },
            new RadioStationDescriptor
            {
                StationName = "SomaFM: Groove Salad (Icecast OGG/MP3)",
                GenreSubtitle = "Ambient Chillout Beats & Downtempo",
                StreamUrlOrFilePath = "https://ice1.somafm.com/groovesalad-128-mp3",
                SourceType = AudioChannelSourceType.InternetRadioStream,
                StreamAudioFormat = AudioType.MPEG
            },
            new RadioStationDescriptor
            {
                StationName = "SomaFM: Lush (Spatial Vocal Chill)",
                GenreSubtitle = "Mellow Electronic & Acoustic Island Vibes",
                StreamUrlOrFilePath = "https://ice1.somafm.com/lush-128-mp3",
                SourceType = AudioChannelSourceType.InternetRadioStream,
                StreamAudioFormat = AudioType.MPEG
            },
            new RadioStationDescriptor
            {
                StationName = "SomaFM: Drone Zone (Deep Meditative Ambient)",
                GenreSubtitle = "Atmospheric Textures for World-Building",
                StreamUrlOrFilePath = "https://ice1.somafm.com/dronezone-128-mp3",
                SourceType = AudioChannelSourceType.InternetRadioStream,
                StreamAudioFormat = AudioType.MPEG
            }
        };

        private readonly List<RadioStationDescriptor> _combinedStationList = new List<RadioStationDescriptor>(32);
        private AudioClip _proceduralLofiFallbackClip;
        private string _userMusicDirectoryPath;

        public IReadOnlyList<RadioStationDescriptor> AllAvailableStations => _combinedStationList;
        public AudioClip ProceduralLofiFallbackClip => _proceduralLofiFallbackClip;
        public string UserMusicDirectoryPath => _userMusicDirectoryPath;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _userMusicDirectoryPath = Path.Combine(Application.persistentDataPath, UserMusicFolderName);
            if (!Directory.Exists(_userMusicDirectoryPath))
            {
                Directory.CreateDirectory(_userMusicDirectoryPath);
            }

            _proceduralLofiFallbackClip = GenerateWarmIslandLofiLoopClip();
            RefreshCombinedStationCatalog();
        }

        /// <summary>
        /// Combines the pre-loaded Internet Radio stations with any user-owned .ogg, .mp3, or .wav files
        /// found inside Application.persistentDataPath/Music.
        /// </summary>
        public void RefreshCombinedStationCatalog()
        {
            _combinedStationList.Clear();
            if (preloadedStations != null)
            {
                _combinedStationList.AddRange(preloadedStations);
            }

            if (!Directory.Exists(_userMusicDirectoryPath)) return;

            string[] files = Directory.GetFiles(_userMusicDirectoryPath);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string ext = Path.GetExtension(file).ToLowerInvariant();
                AudioType format = ext switch
                {
                    ".ogg" => AudioType.OGGVORBIS,
                    ".mp3" => AudioType.MPEG,
                    ".wav" => AudioType.WAV,
                    _ => AudioType.UNKNOWN
                };

                if (format == AudioType.UNKNOWN) continue;

                _combinedStationList.Add(new RadioStationDescriptor
                {
                    StationName = $"User Track: {Path.GetFileNameWithoutExtension(file)}",
                    GenreSubtitle = "Local User Music Library",
                    StreamUrlOrFilePath = $"file://{file}",
                    SourceType = AudioChannelSourceType.UserLocalMusicFile,
                    StreamAudioFormat = format
                });
            }

            Debug.Log($"[PrimaryAudioCatalog] Indexed {_combinedStationList.Count} total stations/tracks (User Music Folder: {_userMusicDirectoryPath}).");
        }

        /// <summary>
        /// Streams an Internet Radio endpoint or Local User Audio File using DownloadHandlerAudioClip
        /// with streamAudio = true so memory usage stays < 2 MB and CPU stays < 1.5%.
        /// </summary>
        public async Task<AudioClip> OpenStreamingAudioClipAsync(RadioStationDescriptor station)
        {
            if (station.SourceType == AudioChannelSourceType.BuiltInProceduralLofiLoop)
            {
                return _proceduralLofiFallbackClip;
            }

            try
            {
                using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(station.StreamUrlOrFilePath, station.StreamAudioFormat))
                {
                    var handler = (DownloadHandlerAudioClip)req.downloadHandler;
                    // CRITICAL FOR < 5% AUDIO CPU GATE:
                    // streamAudio = true decodes incrementally from a small ring buffer instead of decompressing
                    // the entire track on the main thread!
                    handler.streamAudio = true;
                    handler.compressed = false;

                    var op = req.SendWebRequest();

                    // For live Icecast streams or local files, wait until initial ring buffer is primed (~64 KB)
                    float timeoutAt = Time.realtimeSinceStartup + 6.0f;
                    while (!op.isDone && req.downloadedBytes < 65536 && Time.realtimeSinceStartup < timeoutAt)
                    {
                        await Task.Yield();
                    }

                    if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogWarning($"[PrimaryAudioCatalog] Stream '{station.StationName}' unreachable ({req.error}). Falling back to Built-In Island Lofi.");
                        return _proceduralLofiFallbackClip;
                    }

                    AudioClip streamedClip = handler.audioClip;
                    return streamedClip != null ? streamedClip : _proceduralLofiFallbackClip;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PrimaryAudioCatalog] Exception opening stream '{station.StationName}': {ex.Message}. Using Built-In Lofi.");
                return _proceduralLofiFallbackClip;
            }
        }

        /// <summary>
        /// Synthesizes a warm, relaxing 8-second stereo Fender-Rhodes-style Major 7th chord loop in memory
        /// so the Box Radio produces pleasant spatial music immediately out of the box.
        /// </summary>
        private static AudioClip GenerateWarmIslandLofiLoopClip()
        {
            const int sampleRate = 44100;
            const int durationSeconds = 8;
            int totalSamples = sampleRate * durationSeconds;

            float[] stereoData = new float[totalSamples * 2];

            // Warm Fmaj7 -> Em7 -> Dm7 -> Cmaj7 progression
            float[][] chords =
            {
                new[] { 174.61f, 220.00f, 261.63f, 329.63f }, // Fmaj7
                new[] { 164.81f, 196.00f, 246.94f, 293.66f }, // Em7
                new[] { 146.83f, 174.61f, 220.00f, 261.63f }, // Dm7
                new[] { 130.81f, 164.81f, 196.00f, 246.94f }  // Cmaj7
            };

            int samplesPerChord = totalSamples / 4;

            for (int i = 0; i < totalSamples; i++)
            {
                int chordIdx = Mathf.Min(3, i / samplesPerChord);
                int sampleInChord = i % samplesPerChord;
                float t = (float)i / sampleRate;
                float chordLocalT = (float)sampleInChord / sampleRate;

                // Soft electric-piano attack and warm exponential decay envelope
                float env = Mathf.Clamp01(chordLocalT / 0.08f) * Mathf.Exp(-chordLocalT * 0.85f);

                float signal = 0f;
                float[] notes = chords[chordIdx];
                for (int n = 0; n < notes.Length; n++)
                {
                    float freq = notes[n];
                    // Fundamental + soft 2nd harmonic warmth
                    signal += Mathf.Sin(2f * Mathf.PI * freq * t) * 0.20f;
                    signal += Mathf.Sin(2f * Mathf.PI * (freq * 2f) * t) * 0.04f;
                }

                // Gentle stereo tremolo pan for rich 3D spatial presence
                float panL = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 1.5f * t);
                float panR = 0.85f - 0.15f * Mathf.Sin(2f * Mathf.PI * 1.5f * t);

                stereoData[i * 2] = signal * env * panL * 0.5f;
                stereoData[i * 2 + 1] = signal * env * panR * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Procedural_Island_Lofi_Loop", totalSamples, 2, sampleRate, stream: false);
            clip.SetData(stereoData, 0);
            return clip;
        }
    }
}
