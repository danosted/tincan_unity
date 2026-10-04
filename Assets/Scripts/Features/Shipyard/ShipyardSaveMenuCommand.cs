#nullable enable
using TinCan.Core.UI;

namespace TinCan.Features.Shipyard
{
    /// <summary>Shipyard menu: save the design under the name in the Name field.</summary>
    public sealed class ShipyardSaveMenuCommand : IMenuCommand
    {
        public const string Id = "ShipyardSave";

        private readonly IShipyard _shipyard;

        public ShipyardSaveMenuCommand(IShipyard shipyard) => _shipyard = shipyard;

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            if (_shipyard.Save(context.GetValue(ShipyardUseCase.NameField))) context.Menus.Back();
        }
    }
}
