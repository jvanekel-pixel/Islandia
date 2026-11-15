using UnityEngine;

namespace Island.Runtime
{
    /// <summary>
    /// Where gameplay gets the player's head and hands from (ADR-07). Never an XR type.
    /// </summary>
    /// <remarks>
    /// The headset implementation lives in <c>Island.XR</c>; the simulation one is
    /// <see cref="ScriptedInputSource"/>. <see cref="Read"/> is called at least once a frame and
    /// must not allocate (ADR-10's per-frame allocation budget).
    /// </remarks>
    public interface IInputSource
    {
        InputFrame Read();
    }

    /// <summary>One frame of input, in tracking space (relative to the rig's floor origin).</summary>
    public struct InputFrame
    {
        public bool HeadTracked;
        public Pose Head;
        public HandInput Left;
        public HandInput Right;
    }

    /// <summary>One controller or hand.</summary>
    public struct HandInput
    {
        public bool Tracked;
        public Pose Pose;

        /// <summary>0 to 1.</summary>
        public float Trigger;

        /// <summary>0 to 1.</summary>
        public float Grip;

        /// <summary>Thumbstick, each axis −1 to 1.</summary>
        public Vector2 Stick;

        public bool Primary;
        public bool Secondary;
    }
}
