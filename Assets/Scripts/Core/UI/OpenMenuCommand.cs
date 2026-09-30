#nullable enable
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Core.UI
{
    /// <summary>Open the main menu. Handled by <see cref="OpenMenuInputHandler"/>.</summary>
    [CreateAssetMenu(fileName = "Command_OpenMenu", menuName = "TinCan/Input/Commands/Open Menu")]
    public sealed class OpenMenuCommand : InputCommand { }
}
