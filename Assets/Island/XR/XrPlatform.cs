using Island.Runtime;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Island.XR
{
    /// <summary>
    /// Offers the headset to the composition root, when there is one.
    /// </summary>
    /// <remarks>
    /// Registered before the first scene loads. Whether a headset is really there is decided at
    /// boot by asking XR Management for its active loader: on the Quest that is OpenXR; in the
    /// editor and the PlayMode tests there is none (Standalone has no loader, F-05), so the root
    /// boots the simulation instead.
    /// </remarks>
    public static class XrPlatform
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => CompositionRoot.RegisterHeadset(TryCreate);

        /// <summary>The headset platform, or null when no XR loader is running.</summary>
        public static Platform TryCreate()
        {
            var loader = ActiveLoader();
            if (loader == null) return null;
            return new Platform(new XrInputSource(), new UnityClock(), new XrDevice(loader.name));
        }

        private static XRLoader ActiveLoader()
        {
            var settings = XRGeneralSettings.Instance;
            if (settings == null || settings.Manager == null) return null;
            return settings.Manager.activeLoader;
        }

        private sealed class XrDevice : IDeviceServices
        {
            public XrDevice(string loader) => Description = $"headset via {loader}";

            public string Description { get; }
            public bool IsHeadset => true;
        }
    }
}
