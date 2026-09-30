#nullable enable
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Core.UI
{
    /// <summary>Step back one menu (closing the last one). Handled by <see cref="MenuBackInputHandler"/>.</summary>
    [CreateAssetMenu(fileName = "Command_MenuBack", menuName = "TinCan/Input/Commands/Menu Back")]
    public sealed class MenuBackCommand : InputCommand { }
}
