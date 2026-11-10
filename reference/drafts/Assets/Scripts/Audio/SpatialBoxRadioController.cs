using UnityEngine;
using UnityEngine.InputSystem;

namespace IslandBuilder.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SpatialBoxRadioController : MonoBehaviour
    {
        [Header("Physical 3D Controls & VU Faceplate")]
        [SerializeField] private Transform volumeKnobTransform;
        [SerializeField] private Transform stationTuningKnobTransform;
        [SerializeField] private Transform powerButtonTransform;
        [SerializeField] private MeshRenderer vuMeterFaceplateRenderer;

        [Header("3D Spatial Audio Roll-off Settings")]
        [SerializeField] private float minFullVolumeDistanceMeters = 0.85f;
        [SerializeField] private float maxAudibleDistanceMeters = 24.0f;

        private static readonly int AudioRmsLevelShaderId = Shader.PropertyToID("_AudioRmsLevel");
        private static readonly int TuningNormalizedShaderId = Shader.PropertyToID("_TuningNormalized");

        private AudioSource _speakerSource;
        private MaterialPropertyBlock _faceplatePropBlock;

        public AudioSource SpeakerAudioSource => _speakerSource;
        public int CurrentPlaybackSamplePosition => _speakerSource != null && _speakerSource.isPlaying ? _speakerSource.timeSamples : 0;

        private void Awake()
        {
            _speakerSource = GetComponent<AudioSource>();
            _faceplatePropBlock = new MaterialPropertyBlock();

            ConfigureSpatialSpeakerSource();
        }

        private void OnEnable()
        {
            if (SpatialRadioNetworkManager.Instance != null)
            {
                SpatialRadioNetworkManager.Instance.RegisterRadioSpeaker(this);
            }
        }

        private void OnDisable()
        {
            if (SpatialRadioNetworkManager.Instance != null)
            {
                SpatialRadioNetworkManager.Instance.UnregisterRadioSpeaker(this);
            }
        }

        private void ConfigureSpatialSpeakerSource()
        {
            _speakerSource.spatialize = true;
            _speakerSource.spatialBlend = 1.0f; // 100% 3D HRTF Spatial Audio
            _speakerSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _speakerSource.minDistance = minFullVolumeDistanceMeters;
            _speakerSource.maxDistance = maxAudibleDistanceMeters;
            _speakerSource.dopplerLevel = 0.0f; // Disable Doppler pitch warping when player teleports/scales rig!
            _speakerSource.spread = 35.0f;
            _speakerSource.loop = true;
            _speakerSource.playOnAwake = false;
        }

        public void StartSpeakerPlayback(AudioClip sharedClip, float volume, int syncSamplePosition)
        {
            if (_speakerSource == null || sharedClip == null) return;

            if (_speakerSource.clip != sharedClip)
            {
                _speakerSource.clip = sharedClip;
            }

            _speakerSource.volume = volume;
            if (!_speakerSource.isPlaying)
            {
                _speakerSource.Play();
            }

            if (syncSamplePosition > 0 && syncSamplePosition < sharedClip.samples)
            {
                _speakerSource.timeSamples = syncSamplePosition;
            }
        }

        public void StopSpeakerPlayback()
        {
            if (_speakerSource != null && _speakerSource.isPlaying)
            {
                _speakerSource.Stop();
            }
        }

        public void ApplyNetworkVolume(float volume)
        {
            if (_speakerSource != null)
            {
                _speakerSource.volume = volume;
            }

            if (volumeKnobTransform != null)
            {
                float knobAngle = Mathf.Lerp(-135f, 135f, volume);
                volumeKnobTransform.localRotation = Quaternion.Euler(0f, 0f, -knobAngle);
            }
        }

        /// <summary>
        /// Invoked by VR XRSimpleInteractable / Ray Pointer or Physical Hand Touch on the Radio's controls.
        /// </summary>
        public void InteractPressPowerButton()
        {
            SpatialRadioNetworkManager.Instance?.ToggleRadioPower();
        }

        public void InteractCycleNextStationKnob()
        {
            SpatialRadioNetworkManager.Instance?.CycleNextStation();
        }

        public void InteractAdjustVolumeDelta(float delta)
        {
            if (SpatialRadioNetworkManager.Instance == null) return;
            float nextVol = Mathf.Clamp01(SpatialRadioNetworkManager.Instance.MasterRadioVolume + delta);
            SpatialRadioNetworkManager.Instance.SetMasterVolume(nextVol);
        }

        private void Update()
        {
            UpdateVuFaceplateAndKnobs();
            HandleEditorQuickTestingShortcuts();
        }

        private void UpdateVuFaceplateAndKnobs()
        {
            var net = SpatialRadioNetworkManager.Instance;
            if (net == null) return;

            float rms = net.CurrentSmoothedRmsLevel;
            float dial = net.NormalizedStationDialPosition;

            if (vuMeterFaceplateRenderer != null)
            {
                vuMeterFaceplateRenderer.GetPropertyBlock(_faceplatePropBlock);
                _faceplatePropBlock.SetFloat(AudioRmsLevelShaderId, rms);
                _faceplatePropBlock.SetFloat(TuningNormalizedShaderId, dial);
                vuMeterFaceplateRenderer.SetPropertyBlock(_faceplatePropBlock);
            }

            if (stationTuningKnobTransform != null)
            {
                float tuneAngle = Mathf.Lerp(-120f, 120f, dial);
                stationTuningKnobTransform.localRotation = Quaternion.Euler(0f, 0f, -tuneAngle);
            }
        }

        private void HandleEditorQuickTestingShortcuts()
        {
            if (Keyboard.current == null) return;

            // Editor shortcuts: R = Cycle Radio Station, P = Toggle Radio Power, [ / ] = Volume Down/Up
            if (Keyboard.current.rKey.wasPressedThisFrame) InteractCycleNextStationKnob();
            if (Keyboard.current.pKey.wasPressedThisFrame) InteractPressPowerButton();
            if (Keyboard.current.leftBracketKey.wasPressedThisFrame) InteractAdjustVolumeDelta(-0.1f);
            if (Keyboard.current.rightBracketKey.wasPressedThisFrame) InteractAdjustVolumeDelta(+0.1f);
        }
    }
}
