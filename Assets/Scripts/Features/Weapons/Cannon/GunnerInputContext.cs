#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Manning a cannon: active while the local player's body carries State.Occupying.Cannon (the occupy effect,
    /// replicated from the server). It blocks the Humanoid and Camera contexts, so the body neither walks nor turns and
    /// the mouse drives the barrel instead (<see cref="GunnerAimUseCase"/> reads <see cref="Aim"/> into the predicted
    /// input's station aim). Fire and Leave reach the simulation as the Primary and Interact ability bits.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_Gunner", menuName = "TinCan/Input/Gunner Context")]
    public sealed class GunnerInputContext : InputContext
    {
        [Tooltip("Barrel aim delta (Vector2, mouse counts per frame).")]
        public InputActionId? Aim;
        [Tooltip("Fire (button; the Primary ability input).")]
        public InputActionId? Fire;
        [Tooltip("Leave the cannon (button; the Interact ability input).")]
        public InputActionId? Leave;

        protected override IEnumerable<InputActionId?> Slots => new[] { Aim, Fire, Leave };
    }
}
