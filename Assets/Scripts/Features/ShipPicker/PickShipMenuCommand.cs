#nullable enable
using TinCan.Core.UI;

namespace TinCan.Features.ShipPicker
{
    /// <summary>
    /// Ship picker: a design row. Picks the design its row names; on success closes the menus (back to the game), on a
    /// refusal shows the picker again with the reason.
    /// </summary>
    public sealed class PickShipMenuCommand : IMenuCommand
    {
        public const string Id = "PickShip";

        private readonly IShipPicker _picker;

        public PickShipMenuCommand(IShipPicker picker) => _picker = picker;

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            if (!context.ItemId.StartsWith(ShipPickerUseCase.RowPrefix, System.StringComparison.Ordinal)) return;

            if (_picker.Pick(context.ItemId.Substring(ShipPickerUseCase.RowPrefix.Length), out var message))
            {
                context.Menus.CloseAll();
                return;
            }

            context.Close();
            context.Menus.Open(_picker.BuildMenu(message));
        }
    }
}
