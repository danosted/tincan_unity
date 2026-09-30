#nullable enable
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Core.Interaction
{
    /// <summary>Let go of the vehicle and return to the body. Handled by <see cref="ExitVehicleInputHandler"/>.</summary>
    [CreateAssetMenu(fileName = "Command_ExitVehicle", menuName = "TinCan/Input/Commands/Exit Vehicle")]
    public sealed class ExitVehicleCommand : InputCommand { }
}
