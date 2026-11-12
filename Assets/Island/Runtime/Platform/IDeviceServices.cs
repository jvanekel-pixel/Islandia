namespace Island.Runtime
{
    /// <summary>
    /// What the game may ask of the device it is running on (ADR-07). Starts small: later
    /// packages add haptics, refresh rate and the like here, never as direct XR calls.
    /// </summary>
    public interface IDeviceServices
    {
        /// <summary>A short human description, for the boot log.</summary>
        string Description { get; }

        /// <summary>True on a real headset; false in the editor and in tests.</summary>
        bool IsHeadset { get; }
    }
}
