#nullable enable
using TinCan.Core.UI;

namespace TinCan.Features.Shipyard
{
    /// <summary>Shipyard menu: load the design named in the Name field.</summary>
    public sealed class ShipyardLoadMenuCommand : IMenuCommand
    {
        public const string Id = "ShipyardLoad";

        private readonly IShipyard _shipyard;

        public ShipyardLoadMenuCommand(IShipyard shipyard) => _shipyard = shipyard;

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            if (_shipyard.Load(context.GetValue(ShipyardUseCase.NameField))) context.Menus.Back();
        }
    }
}
