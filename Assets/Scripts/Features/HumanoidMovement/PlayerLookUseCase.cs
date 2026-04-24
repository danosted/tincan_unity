using UnityEngine;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using System.Collections.Generic;
using System.Linq;
using TinCan.Features.FreeCamera;
using TinCan.Features.Possession;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Application Layer: Coordinates input for looking around.
    /// Renamed internally to support generic orbital cameras across different actors.
    /// </summary>
    public class PlayerLookUseCase : ITickable
    {
        private readonly IInputService _inputService;
        private readonly INetworkService _networkService;
        private readonly IActorRegistry _registry;

        public PlayerLookUseCase(
            IInputService inputService,
            INetworkService networkService,
            IActorRegistry registry)
        {
            _inputService = inputService;
            _networkService = networkService;
            _registry = registry;
        }

        public void Tick()
        {
            Vector2 mouseDelta = _inputService.GetMouseDelta();
            if (mouseDelta.sqrMagnitude < 0.001f) return;

            ulong localId = _networkService.LocalClientId;

            // Process all actors with an orbital camera
            foreach (var character in _registry.GetActors<IHasOrbitalCamera>())
            {
                if (!character.IsSimulating) continue;

                // Only process look if the local player currently controls this actor
                if (character is IPossessable possessable && !possessable.IsCapturedBy(localId)) continue;

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
