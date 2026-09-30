#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// At the helm: active while the local player possesses the airship. <see cref="AirshipMovementUseCase"/> reads the
    /// slots into the owner-written <see cref="AirshipInputState"/>. Cancel lets go of the helm (a route to the
    /// ExitVehicle command).
    /// </summary>
    [CreateAssetMenu(fileName = "Context_Airship", menuName = "TinCan/Input/Airship Context")]
    public sealed class AirshipInputContext : InputContext
    {
        [Tooltip("Forward/back throttle (axis, -1..1).")]
        public InputActionId? Throttle;
        [Tooltip("Turn right/left (axis, -1..1).")]
        public InputActionId? Yaw;
        [Tooltip("Nose down/up (axis, -1..1; positive pitches down).")]
        public InputActionId? Pitch;

        protected override IEnumerable<InputActionId?> Slots => new[] { Throttle, Yaw, Pitch };
    }
}
