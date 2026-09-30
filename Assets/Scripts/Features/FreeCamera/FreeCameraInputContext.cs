#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.FreeCamera
{
    /// <summary>
    /// Flying the spectator camera: active while the local player possesses something that is neither a body nor the
    /// airship. <see cref="FreeCameraMovementUseCase"/> reads <see cref="Move"/> (and the Camera context's Look); Cancel
    /// frees or recaptures the cursor (<see cref="ToggleCursorCommand"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "Context_FreeCamera", menuName = "TinCan/Input/Free Camera Context")]
    public sealed class FreeCameraInputContext : InputContext
    {
        [Tooltip("Fly direction (Vector2, x right, y forward).")]
        public InputActionId? Move;

        protected override IEnumerable<InputActionId?> Slots => new[] { Move };
    }
}
