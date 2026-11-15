using UnityEngine;

namespace Island.Runtime
{
    /// <summary>The real clock: one frame per <see cref="Advance"/>, as long as Unity says it was.</summary>
    public sealed class UnityClock : IClock
    {
        public long Frame { get; private set; }
        public double Seconds { get; private set; }
        public float DeltaSeconds { get; private set; }

        public void Advance()
        {
            Frame++;
            DeltaSeconds = Time.deltaTime;
            Seconds += DeltaSeconds;
        }
    }
}
