#nullable enable
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Core.Possession
{
    /// <summary>Cycle to the next thing the player may control. Handled by <see cref="SwitchPossessionInputHandler"/>.</summary>
    [CreateAssetMenu(fileName = "Command_SwitchPossession", menuName = "TinCan/Input/Commands/Switch Possession")]
    public sealed class SwitchPossessionCommand : InputCommand { }
}
