using System;

namespace Island.Runtime
{
    /// <summary>
    /// The services one kind of device provides: a headset, or the simulation that stands in for
    /// one in the editor and the PlayMode tests.
    /// </summary>
    public sealed class Platform
    {
        public Platform(IInputSource input, IClock clock, IDeviceServices device)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public IInputSource Input { get; }
        public IClock Clock { get; }
        public IDeviceServices Device { get; }

        /// <summary>True whenever there is no headset behind this platform.</summary>
        public bool IsSimulation => !Device.IsHeadset;
    }
}
