namespace Island.Runtime
{
    /// <summary>
    /// Everything the composition root has built, in one place. Gameplay is handed what it needs
    /// from here; nothing goes looking for services in the scene.
    /// </summary>
    public sealed class Services
    {
        public Services(Platform platform, IRig rig)
        {
            Platform = platform;
            Rig = rig;
        }

        public Platform Platform { get; }
        public IRig Rig { get; }

        public IInputSource Input => Platform.Input;
        public IClock Clock => Platform.Clock;
        public IDeviceServices Device => Platform.Device;

        /// <summary>The input read at the start of the current frame.</summary>
        public InputFrame LatestInput { get; internal set; }
    }
}
