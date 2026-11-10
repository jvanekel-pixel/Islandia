using UnityEngine;

namespace IslandBuilder.Media
{
    public sealed class OpenXrCinemaQuadLayerRouter : MonoBehaviour
    {
        [Header("OpenXR Quad Composition Layer Transform")]
        [Tooltip("Transform of the Unity OpenXR CompositionLayer / OVROverlay Quad GameObject.")]
        [SerializeField] private Transform compositionQuadLayerTransform;

        private VirtualCinemaScreenController _trackedScreen;

        private void Start()
        {
            if (VirtualCinemaManager.Instance != null)
            {
                VirtualCinemaManager.Instance.OnPrimaryCompositorScreenChanged += HandlePrimaryScreenChanged;
                HandlePrimaryScreenChanged(VirtualCinemaManager.Instance.CurrentPrimaryCompositorScreen);
            }
        }

        private void OnDestroy()
        {
            if (VirtualCinemaManager.Instance != null)
            {
                VirtualCinemaManager.Instance.OnPrimaryCompositorScreenChanged -= HandlePrimaryScreenChanged;
            }
        }

        private void HandlePrimaryScreenChanged(VirtualCinemaScreenController newPrimaryScreen)
        {
            _trackedScreen = newPrimaryScreen;
            if (compositionQuadLayerTransform != null)
            {
                bool hasActivePrimary = _trackedScreen != null && _trackedScreen.IsPlaying;
                if (compositionQuadLayerTransform.gameObject.activeSelf != hasActivePrimary)
                {
                    compositionQuadLayerTransform.gameObject.SetActive(hasActivePrimary);
                }
            }
        }

        private void LateUpdate()
        {
            if (_trackedScreen == null || !_trackedScreen.IsPlaying || compositionQuadLayerTransform == null)
            {
                return;
            }

            Transform surface = _trackedScreen.ScreenSurfaceTransform;
            compositionQuadLayerTransform.SetPositionAndRotation(surface.position, surface.rotation);
            compositionQuadLayerTransform.localScale = surface.lossyScale;
        }
    }
}
