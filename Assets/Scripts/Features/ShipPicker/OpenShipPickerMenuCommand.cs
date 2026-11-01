#nullable enable
using TinCan.Core.UI;

namespace TinCan.Features.ShipPicker
{
    /// <summary>Main menu: open the ship picker over it, listing the designs stored right now. Back returns to the menu.</summary>
    public sealed class OpenShipPickerMenuCommand : IMenuCommand
    {
        public const string Id = "OpenShipPicker";

        private readonly IShipPicker _picker;

        public OpenShipPickerMenuCommand(IShipPicker picker) => _picker = picker;

        public string CommandId => Id;

        public void Execute(MenuContext context) => context.Menus.Open(_picker.BuildMenu());
    }
}
