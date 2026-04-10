using UnityEngine;
using VContainer.Unity;
using TinCan.Core.Domain;
using System.Collections.Generic;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Application Layer: Coordinates input for looking around.
    /// </summary>
    public class HumanoidLookUseCase : ITickable
    {
        private readonly IInputService _inputService;
        private readonly IEnumerable<IHumanoidLookView> _views;

        public HumanoidLookUseCase(IInputService inputService, IEnumerable<IHumanoidLookView> views)
        {
            _inputService = inputService;
            _views = views;
        }

        public void Tick()
        {
            Vector2 mouseDelta = _inputService.GetMouseDelta();
            if (mouseDelta.sqrMagnitude < 0.001f) return;

            foreach (var view in _views)
            {
                if (!view.IsActive) continue;

                float newYaw = view.Yaw + (mouseDelta.x * view.Sensitivity);
                float newPitch = Mathf.Clamp(view.Pitch - (mouseDelta.y * view.Sensitivity), -view.MaxPitch, view.MaxPitch);

                view.ApplyLook(newPitch, newYaw);
            }
        }
    }
}
