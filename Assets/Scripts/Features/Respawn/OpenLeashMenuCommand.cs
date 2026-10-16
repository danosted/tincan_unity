#nullable enable
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.Respawn
{
    /// <summary>Input command: while hanging on a leash, open the respawn choice (routed from Interact by the Leashed context).</summary>
    [CreateAssetMenu(fileName = "Command_OpenLeashMenu", menuName = "TinCan/Input/Commands/Open Leash Menu")]
    public sealed class OpenLeashMenuCommand : InputCommand { }
}
