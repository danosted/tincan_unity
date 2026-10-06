#nullable enable
using TinCan.Core.UI;

namespace TinCan.Features.ShipSockets
{
    /// <summary>Fitting menu row: ask the server to mount the row's fitting (its item id) in the chosen socket.</summary>
    public sealed class MountFittingMenuCommand : IMenuCommand
    {
        public const string Id = "MountFitting";

        private readonly ShipFittingChoice _choice;

        public MountFittingMenuCommand(ShipFittingChoice choice) => _choice = choice;

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            if (_choice.TryTake(out var state, out var socket)) state.RequestMount(socket, context.ItemId);
            context.Menus.CloseAll();
        }
    }
}
