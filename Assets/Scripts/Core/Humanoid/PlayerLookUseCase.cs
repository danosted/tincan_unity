#nullable enable
using UnityEngine;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Domain.Look;

namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// Application Layer: turns the Camera context's Look into the orbital camera of whatever the local player controls
    /// (their body or the airship). While the Camera context is blocked (a menu, a station that aims with the mouse)
    /// Look reads zero and the camera holds still.
    /// </summary>
    public class PlayerLookUseCase : ITickable
    {
        private readonly IInputReader _input;
        private readonly CameraInputContext _controls;
        private readonly INetworkService _networkService;
        private readonly IActorRegistry _registry;

        public PlayerLookUseCase(
            IInputReader input,
            CameraInputContext controls,
            INetworkService networkService,
            IActorRegistry registry)
        {
            _input = input;
            _controls = controls;
            _networkService = networkService;
            _registry = registry;
        }

        public void Tick()
        {
            Vector2 mouseDelta = _input.ReadVector2(_controls.Look);
            if (mouseDelta.sqrMagnitude < 0.001f) return;

            ulong localId = _networkService.LocalClientId;

            // Process all actors with an orbital camera
            foreach (var character in _registry.GetActors<IHasOrbitalCamera>())
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

        private void ApplyLook(IOrbitalLookView view, Vector2 mouseDelta)
        {
            float newYaw = view.Yaw + (mouseDelta.x * view.Sensitivity);
            float newPitch = Mathf.Clamp(view.Pitch - (mouseDelta.y * view.Sensitivity), -view.MaxPitch, view.MaxPitch);

            view.ApplyLook(newPitch, newYaw);
        }
    }
}
