namespace Island.Runtime
{
    /// <summary>
    /// The platform the game runs on with no headset: the editor, and every PlayMode test (ADR-07).
    /// </summary>
    public static class SimulationPlatform
    {
        /// <summary>Scripted input and a scripted clock, supplied by a test.</summary>
        public static Platform Create(IInputSource input, IClock clock) =>
            new Platform(input, clock, new SimulatedDevice());

        /// <summary>What boots when nothing else is offered: a player standing still on a fixed step.</summary>
        public static Platform Idle()
        {
            var clock = new ScriptedClock();
            return Create(ScriptedInputSource.StandingStill(clock), clock);
        }

        private sealed class SimulatedDevice : IDeviceServices
        {
            public string Description => "simulation";
            public bool IsHeadset => false;
        }
    }
}
