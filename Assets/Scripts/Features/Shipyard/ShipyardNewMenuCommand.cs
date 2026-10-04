#nullable enable
using TinCan.Core.UI;

namespace TinCan.Features.Shipyard
{
    /// <summary>Shipyard menu: start a new design (a helm on a deck).</summary>
    public sealed class ShipyardNewMenuCommand : IMenuCommand
    {
        public const string Id = "ShipyardNew";

        private readonly IShipyard _shipyard;

        public ShipyardNewMenuCommand(IShipyard shipyard) => _shipyard = shipyard;

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            _shipyard.New();
            context.Menus.Back();
        }
    }
}
