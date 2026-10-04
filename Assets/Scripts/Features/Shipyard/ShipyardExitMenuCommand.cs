#nullable enable
using TinCan.Core.Domain.Networking;
using TinCan.Core.UI;

namespace TinCan.Features.Shipyard
{
    /// <summary>Shipyard menu: leave the shipyard, back to the main menu when offline.</summary>
    public sealed class ShipyardExitMenuCommand : IMenuCommand
    {
        public const string Id = "ShipyardExit";

        private readonly IShipyard _shipyard;
        private readonly INetworkService _network;
        private readonly MenuDefinition _mainMenu;

        public ShipyardExitMenuCommand(IShipyard shipyard, INetworkService network, MenuDefinition mainMenu)
        {
            _shipyard = shipyard;
            _network = network;
            _mainMenu = mainMenu;
        }

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            _shipyard.Close();
            context.Menus.CloseAll();
            if (!_network.IsActive) context.Menus.Open(_mainMenu);
        }
    }
}
