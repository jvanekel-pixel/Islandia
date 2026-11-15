using System;
using UnityEngine;

namespace Island.Runtime
{
    /// <summary>
    /// The one place the game is put together (ADR-06, ADR-07). The boot scene holds this and the
    /// rig; everything else is constructed here, in code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The platform is chosen once, in <see cref="Awake"/>, in this order: one a test asked for
    /// with <see cref="UseForNextBoot"/>; the headset, if <c>Island.XR</c> registered one and an
    /// XR loader is running; otherwise <see cref="SimulationPlatform.Idle"/>. So the editor and
    /// every PlayMode test run the whole game with no XR loader at all.
    /// </para>
    /// <para>
    /// Each frame it advances the clock, reads input once, and puts the head where the input says.
    /// On a headset the head is read again just before rendering, so the picture uses the newest
    /// pose rather than one a frame old.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class CompositionRoot : MonoBehaviour
    {
        private static Platform s_nextBoot;
        private static Func<Platform> s_headset;

        /// <summary>The root of the running game, or null when none is booted.</summary>
        public static CompositionRoot Current { get; private set; }

        /// <summary>What was built at boot. Null until <see cref="Awake"/> has run.</summary>
        public Services Services { get; private set; }

        /// <summary>
        /// Boot the next composition root on this platform instead of choosing one. Used once.
        /// </summary>
        public static void UseForNextBoot(Platform platform) =>
            s_nextBoot = platform ?? throw new ArgumentNullException(nameof(platform));

        /// <summary>Forgets a platform passed to <see cref="UseForNextBoot"/> that was never used.</summary>
        public static void ClearNextBoot() => s_nextBoot = null;

        /// <summary>
        /// Called by <c>Island.XR</c> at startup. The factory returns null when no headset is
        /// running, and the root then falls back to simulation.
        /// </summary>
        public static void RegisterHeadset(Func<Platform> factory) => s_headset = factory;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError("[Island] A second CompositionRoot was created; it has been removed.");
                Destroy(gameObject);
                return;
            }

            var platform = s_nextBoot ?? s_headset?.Invoke() ?? SimulationPlatform.Idle();
            s_nextBoot = null;

            var rigs = FindObjectsByType<SceneRig>(FindObjectsSortMode.None);
            if (rigs.Length != 1 || rigs[0].Head == null)
            {
                // Thrown, not logged: a boot scene without exactly one rig and its camera was not
                // made by BootSceneBuilder, and nothing after this point can work.
                throw new InvalidOperationException(
                    $"[Island] The boot scene needs exactly one rig with a camera; found {rigs.Length} rig(s).");
            }

            Services = new Services(platform, rigs[0]);
            Current = this;
            Debug.Log($"[Island] Booted on {platform.Device.Description} ({(platform.IsSimulation ? "simulation" : "headset")}).");
        }

        private void OnEnable() => Application.onBeforeRender += BeforeRender;

        private void OnDisable() => Application.onBeforeRender -= BeforeRender;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void Update()
        {
            if (Services == null) return;
            Services.Clock.Advance();
            var input = Services.Input.Read();
            Services.LatestInput = input;
            PlaceHead(input);
        }

        private void BeforeRender()
        {
            if (Services == null || Services.Platform.IsSimulation) return;
            PlaceHead(Services.Input.Read());
        }

        private void PlaceHead(in InputFrame input)
        {
            if (!input.HeadTracked) return;
            Services.Rig.Head.SetLocalPositionAndRotation(input.Head.position, input.Head.rotation);
        }
    }
}
