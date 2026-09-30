#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// On foot: active while the local player possesses their body. <see cref="HumanoidMovementUseCase"/> reads Move,
    /// Jump and Sprint into the predicted <see cref="HumanoidInputState"/>; Sprint, Interact, Primary and Secondary also
    /// reach the simulation as ability-input bits (their <c>GameplayInput</c> lists them). Stations that take over the
    /// body's controls block this context.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_Humanoid", menuName = "TinCan/Input/Humanoid Context")]
    public sealed class HumanoidInputContext : InputContext
    {
        [Tooltip("Walk direction (Vector2, x right, y forward).")]
        public InputActionId? Move;
        [Tooltip("Jump (button).")]
        public InputActionId? Jump;
        [Tooltip("Sprint while held (button; also the Sprint ability input).")]
        public InputActionId? Sprint;
        [Tooltip("Use what you look at (button; the Interact ability input).")]
        public InputActionId? Interact;
        [Tooltip("Use the held item or ability (button; the Primary ability input).")]
        public InputActionId? Primary;
        [Tooltip("Secondary use (button; the Secondary ability input).")]
        public InputActionId? Secondary;

        protected override IEnumerable<InputActionId?> Slots => new[] { Move, Jump, Sprint, Interact, Primary, Secondary };
    }
}
