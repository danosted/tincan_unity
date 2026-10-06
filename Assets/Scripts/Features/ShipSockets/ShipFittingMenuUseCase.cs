#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.UI;
using VContainer.Unity;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// This peer, per frame: when the server asks this player to choose a fitting for a socket, opens the fitting menu,
    /// one row per fitting the loaded features offer (and "Leave it empty"). Picking a row sends the mount request
    /// (<see cref="MountFittingMenuCommand"/>).
    /// </summary>
    public sealed class ShipFittingMenuUseCase : ITickable
    {
        public const string MenuId = "Fittings";

        private readonly IActorRegistry _actors;
        private readonly IShipFittingCatalog _catalog;
        private readonly IMenuSystem _menus;
        private readonly ShipFittingChoice _choice;
        private MenuDefinition? _menu;

        public ShipFittingMenuUseCase(IActorRegistry actors, IShipFittingCatalog catalog, IMenuSystem menus, ShipFittingChoice choice)
        {
            _actors = actors;
            _catalog = catalog;
            _menus = menus;
            _choice = choice;
        }

        public void Tick()
        {
            foreach (var state in _actors.GetActors<IShipFittingState>().ToList())
            {
                if (!state.TryTakeChooser(out var socket)) continue;

                _choice.Begin(state, socket);
                _menus.Open(_menu ??= Menu());
            }
        }

        private MenuDefinition Menu()
        {
            var rows = _catalog.Fittings
                .Select(f => new MenuItemDefinition
                {
                    ItemId = f.FittingId, Label = f.DisplayName, Kind = MenuItemKind.Command, CommandId = MountFittingMenuCommand.Id,
                })
                .Append(new MenuItemDefinition { ItemId = "back", Label = "Leave it empty", Kind = MenuItemKind.Back });
            return MenuDefinition.Create(MenuId, "Mount a fitting", rows.ToArray());
        }
    }
}
