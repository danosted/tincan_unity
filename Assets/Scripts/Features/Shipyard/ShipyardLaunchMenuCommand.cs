#nullable enable
using TinCan.Core.UI;

namespace TinCan.Features.Shipyard
{
    /// <summary>Shipyard menu: fly the design (hosts a session, or rebuilds the ships in play as the host).</summary>
    public sealed class ShipyardLaunchMenuCommand : IMenuCommand
    {
        public const string Id = "ShipyardLaunch";

        private readonly IShipyard _shipyard;

        public ShipyardLaunchMenuCommand(IShipyard shipyard) => _shipyard = shipyard;

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            if (_shipyard.Launch()) context.Menus.CloseAll();
            else context.Menus.Back();
        }
    }
}
