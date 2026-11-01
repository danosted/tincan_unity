#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain.Settings;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Domain.Look;

namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// Application Layer: turns the Camera context's Look into the look of whatever the local player controls
    /// (their body or the airship). While the Camera context is blocked (a menu, a station that aims with the mouse)
    /// Look reads zero and the camera holds still.
    /// </summary>
    public class PlayerLookUseCase : ITickable
    {
        private readonly IInputReader _input;
        private readonly CameraInputContext _controls;
        private readonly INetworkService _networkService;
        private readonly IActorRegistry _registry;
        private readonly IPlayerSettings? _settings;

        public PlayerLookUseCase(
            IInputReader input,
            CameraInputContext controls,
            INetworkService networkService,
            IActorRegistry registry) : this(input, controls, networkService, registry, null) { }

        /// <summary>With the player's settings: their sensitivity scales each view's own, and invert Y flips the pitch.</summary>
        [Inject]
        public PlayerLookUseCase(
            IInputReader input,
            CameraInputContext controls,
            INetworkService networkService,
            IActorRegistry registry,
            IPlayerSettings? settings)
        {
            _input = input;
            _controls = controls;
            _networkService = networkService;
            _registry = registry;
            _settings = settings;
        }

        public void Tick()
        {
            Vector2 raw = _input.ReadVector2(_controls.Look);
            if (raw.sqrMagnitude < 0.001f) return;
            Vector2 mouseDelta = LookSettings.Apply(_settings, raw);

            ulong localId = _networkService.LocalClientId;

            // Process all actors with a look
            foreach (var character in _registry.GetActors<IHasLook>())
            {
                // The camera belongs to whoever controls the actor, whichever peer simulates it: a client pilot does
                // not simulate the ship (the server does), yet the ship's camera is theirs.
                if (character is IPossessable possessable)
                {
                    if (!possessable.IsCapturedBy(localId)) continue;
                }
                else if (!character.IsSimulating) continue;

                ApplyLook(character.Look, mouseDelta);
            }
        }

        private void ApplyLook(ILookView view, Vector2 mouseDelta)
        {
            float newYaw = view.Yaw + (mouseDelta.x * view.Sensitivity);
            float newPitch = Mathf.Clamp(view.Pitch - (mouseDelta.y * view.Sensitivity), -view.MaxPitch, view.MaxPitch);

            view.ApplyLook(newPitch, newYaw);
        }
    }
}
