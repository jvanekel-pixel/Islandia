using System;

namespace Island.Runtime
{
    /// <summary>
    /// Input written by a test: a function from the clock to the frame the player "did".
    /// </summary>
    /// <remarks>
    /// Driven by the clock rather than by how many times it is read, so reading it twice in a
    /// frame (the headset path re-reads the head just before rendering) cannot skip ahead.
    /// </remarks>
    public sealed class ScriptedInputSource : IInputSource
    {
        private readonly IClock _clock;
        private readonly Func<IClock, InputFrame> _script;

        public ScriptedInputSource(IClock clock, Func<IClock, InputFrame> script)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _script = script ?? throw new ArgumentNullException(nameof(script));
        }

        /// <summary>A player standing still, eyes 1.6 m above the floor, looking ahead.</summary>
        public static ScriptedInputSource StandingStill(IClock clock) =>
            new ScriptedInputSource(clock, _ => new InputFrame
            {
                HeadTracked = true,
                Head = new UnityEngine.Pose(new UnityEngine.Vector3(0f, 1.6f, 0f), UnityEngine.Quaternion.identity),
            });

        public InputFrame Read() => _script(_clock);
    }
}
