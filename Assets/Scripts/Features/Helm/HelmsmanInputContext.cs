#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.Helm
{
    /// <summary>
    /// At the helm: active while the local player's body carries State.Occupying.Helm (the occupy effect, replicated
    /// from the server). It blocks the Humanoid context, so the body does not walk, but not the Camera context: the
    /// helmsman looks around freely while steering. <see cref="HelmInputUseCase"/> reads the axes into the predicted
    /// input's station axes; Leave reaches the simulation as the Interact ability bit.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_Helmsman", menuName = "TinCan/Input/Helmsman Context")]
    public sealed class HelmsmanInputContext : InputContext
    {
        [Tooltip("Forward/back throttle (axis, -1..1).")]
        public InputActionId? Throttle;
        [Tooltip("Turn right/left (axis, -1..1).")]
        public InputActionId? Yaw;
        [Tooltip("Nose down/up (axis, -1..1; positive pitches down).")]
        public InputActionId? Pitch;
        [Tooltip("Let go of the helm (button; the Interact ability input).")]
        public InputActionId? Leave;

        protected override IEnumerable<InputActionId?> Slots => new[] { Throttle, Yaw, Pitch, Leave };
    }
}
