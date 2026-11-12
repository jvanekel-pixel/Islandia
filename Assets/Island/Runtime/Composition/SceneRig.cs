using UnityEngine;

namespace Island.Runtime
{
    /// <summary>
    /// Marks the rig in the boot scene and finds its parts by structure, so nothing in the scene
    /// holds a serialised reference that someone would have to wire (ADR-06).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneRig : MonoBehaviour, IRig
    {
        private Transform _head;

        public Transform Origin => transform;

        public Transform Head
        {
            get
            {
                if (_head == null)
                {
                    var camera = GetComponentInChildren<Camera>(true);
                    _head = camera != null ? camera.transform : null;
                }
                return _head;
            }
        }
    }
}
