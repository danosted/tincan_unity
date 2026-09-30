#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.FreeCamera
{
    /// <summary>Frees or recaptures the cursor while a free camera is flying; declines otherwise.</summary>
    public sealed class ToggleCursorInputHandler : InputCommandHandler<ToggleCursorCommand>
    {
        private readonly IActorRegistry _registry;

        public ToggleCursorInputHandler(IActorRegistry registry)
        {
            _registry = registry;
        }

        protected override bool Handle(ToggleCursorCommand command)
        {
            bool anyActive = false;
            foreach (var view in _registry.GetActors<IFreeCameraView>()) anyActive |= view.IsActive;
            if (!anyActive) return false;

            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
            return true;
        }
    }
}
