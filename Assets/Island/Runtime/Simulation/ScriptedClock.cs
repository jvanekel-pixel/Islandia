using System;

namespace Island.Runtime
{
    /// <summary>
    /// A clock with a fixed step, whatever the real frame took. Frame <c>n</c> is always at
    /// <c>n × step</c> seconds, so a scripted run is the same run every time.
    /// </summary>
    public sealed class ScriptedClock : IClock
    {
        /// <summary>The Quest 2's default refresh rate, as a step.</summary>
        public const double DefaultStep = 1.0 / 72.0;

        private readonly double _step;

        public ScriptedClock(double stepSeconds = DefaultStep)
        {
            if (!(stepSeconds > 0) || double.IsInfinity(stepSeconds))
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), stepSeconds, "must be a positive number of seconds");
            _step = stepSeconds;
        }

        public long Frame { get; private set; }
        public double Seconds => Frame * _step;
        public float DeltaSeconds => Frame == 0 ? 0f : (float)_step;

        public void Advance() => Frame++;
    }
}
