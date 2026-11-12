using UnityEngine;

namespace Island.Runtime
{
    /// <summary>The player's body in the scene: where they stand and where their eyes are.</summary>
    public interface IRig
    {
        /// <summary>The floor origin of tracking space. Moving this moves the player.</summary>
        Transform Origin { get; }

        /// <summary>The camera. Its local pose is the head pose from <see cref="IInputSource"/>.</summary>
        Transform Head { get; }
    }
}
