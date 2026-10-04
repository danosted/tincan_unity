#nullable enable
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>Cancel in the shipyard: open its menu (save, load, new, launch, exit).</summary>
    [CreateAssetMenu(fileName = "Command_OpenShipyardMenu", menuName = "TinCan/Input/Commands/Open Shipyard Menu")]
    public sealed class OpenShipyardMenuCommand : InputCommand
    {
    }
}
