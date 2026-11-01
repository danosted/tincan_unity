#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using TinCan.Core.UI;

namespace TinCan.Features.FreeCamera
{
    /// <summary>
    /// Frees or recaptures the cursor while a free camera is flying (an ask to <see cref="ICursorPolicy"/>, released by
    /// <see cref="FreeCameraMovementUseCase"/> when the camera stops); declines otherwise.
    /// </summary>
    public sealed class ToggleCursorInputHandler : InputCommandHandler<ToggleCursorCommand>
    {
        public const string CursorOwner = "FreeCamera";

        private readonly IActorRegistry _registry;
        private readonly ICursorPolicy _cursor;

        public ToggleCursorInputHandler(IActorRegistry registry, ICursorPolicy cursor)
        {
            _registry = registry;
            _cursor = cursor;
        }

        protected override bool Handle(ToggleCursorCommand command)
        {
            bool anyActive = false;
            foreach (var view in _registry.GetActors<IFreeCameraView>()) anyActive |= view.IsActive;
            if (!anyActive) return false;

            _cursor.Request(CursorOwner, _cursor.Mode == CursorState.Locked ? CursorState.Free : CursorState.Locked);
            return true;
        }
    }
}
