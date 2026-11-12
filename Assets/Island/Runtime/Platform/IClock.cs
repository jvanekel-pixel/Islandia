namespace Island.Runtime
{
    /// <summary>
    /// Game time, advanced once per frame by the <see cref="CompositionRoot"/> (ADR-07).
    /// </summary>
    /// <remarks>
    /// Gameplay reads time from here and never from <c>UnityEngine.Time</c>, so a PlayMode test
    /// can run the game on a fixed, scripted step and get the same answer every time. This is the
    /// frame clock; the StreamBox client's wall-clock <c>Island.Core.StreamBox.IClock</c> is a
    /// different thing with a different job, and the two are kept apart by namespace.
    /// </remarks>
    public interface IClock
    {
        /// <summary>Frames advanced since boot. Zero before the first frame.</summary>
        long Frame { get; }

        /// <summary>Seconds of game time since boot.</summary>
        double Seconds { get; }

        /// <summary>Seconds the most recent frame covered.</summary>
        float DeltaSeconds { get; }

        /// <summary>Moves time on by one frame. Called by the composition root only.</summary>
        void Advance();
    }
}
