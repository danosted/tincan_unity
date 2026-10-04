#nullable enable
using TinCan.Core.UI;

namespace TinCan.Features.Shipyard
{
    /// <summary>Main menu: open the shipyard.</summary>
    public sealed class EnterShipyardMenuCommand : IMenuCommand
    {
        public const string Id = "EnterShipyard";

        private readonly IShipyard _shipyard;

        public EnterShipyardMenuCommand(IShipyard shipyard) => _shipyard = shipyard;

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            context.Menus.CloseAll();
            _shipyard.Open();
        }
    }
}
