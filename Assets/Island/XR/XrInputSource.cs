using Island.Runtime;
using UnityEngine;
using UnityEngine.XR;

namespace Island.XR
{
    /// <summary>
    /// The headset's head and controllers, read through Unity's XR input devices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses the engine's <c>UnityEngine.XR.InputDevices</c> rather than the Input System, because
    /// it works whichever input handler the project is set to and costs no allocation: devices are
    /// structs, looked up by node.
    /// </para>
    /// <para>
    /// This is the seam, not the finished controls. Interaction (rays, grabbing, locomotion) lands
    /// in later packages on top of <see cref="IInputSource"/>; whether these readings are right on
    /// a real Quest 2 is a <c>needs-device</c> question for HG-1.
    /// </para>
    /// </remarks>
    public sealed class XrInputSource : IInputSource
    {
        public InputFrame Read()
        {
            var frame = new InputFrame();
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            frame.HeadTracked = TryPose(head, out frame.Head);
            frame.Left = ReadHand(XRNode.LeftHand);
            frame.Right = ReadHand(XRNode.RightHand);
            return frame;
        }

        private static HandInput ReadHand(XRNode node)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            var hand = new HandInput();
            hand.Tracked = TryPose(device, out hand.Pose);
            if (!device.isValid) return hand;

            device.TryGetFeatureValue(CommonUsages.trigger, out hand.Trigger);
            device.TryGetFeatureValue(CommonUsages.grip, out hand.Grip);
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out hand.Stick);
            device.TryGetFeatureValue(CommonUsages.primaryButton, out hand.Primary);
            device.TryGetFeatureValue(CommonUsages.secondaryButton, out hand.Secondary);
            return hand;
        }

        private static bool TryPose(InputDevice device, out Pose pose)
        {
            pose = Pose.identity;
            if (!device.isValid) return false;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position)) return false;
            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)) return false;
            pose = new Pose(position, rotation);
            return true;
        }
    }
}
