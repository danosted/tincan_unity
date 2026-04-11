using UnityEngine;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Application Layer: Coordinates input for looking around.
    /// </summary>
    public class HumanoidLookUseCase : ITickable
    {
        private readonly IInputService _inputService;
        private readonly INetworkService _networkService;
        private readonly IActorRegistry _registry;

        public HumanoidLookUseCase(
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

            // Process all complete characters (Facade pattern or Mediator)
            foreach (var character in _registry.GetActors<IHumanoidCharacterView>())
            {
                if (!character.IsSimulating || !character.IsCapturedBy(localId)) continue;
                ApplyLook(character.Look, mouseDelta);
            }
        }

        private void ApplyLook(IHumanoidLookView view, Vector2 mouseDelta)
        {
            float newYaw = view.Yaw + (mouseDelta.x * view.Sensitivity);
            float newPitch = Mathf.Clamp(view.Pitch - (mouseDelta.y * view.Sensitivity), -view.MaxPitch, view.MaxPitch);

            view.ApplyLook(newPitch, newYaw);
        }
    }
}
