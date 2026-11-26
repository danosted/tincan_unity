#nullable enable
using TinCan.Core.UI;
using VContainer.Unity;

namespace TinCan.Features.ShipLoss
{
    /// <summary>
    /// Presentation, every peer: while the ship is lost, the Lost screen is up (Respawn, any rows another feature adds
    /// through the <see cref="ShipLossMenus.ChoicesSource"/> row source, Quit) as a pinned modal request, so Esc cannot
    /// drop a player into a dead ship (it opens the pause menu over the screen instead). It goes when the ship is back.
    /// </summary>
    public sealed class ShipLostScreenPresenter : ITickable
    {
        public const string MenuOwner = "ShipLoss.Lost";

        private readonly IShipLoss _loss;
        private readonly IMenuSystem _menus;
        private readonly MenuDefinition _lostMenu;

        public ShipLostScreenPresenter(IShipLoss loss, IMenuSystem menus, MenuDefinition lostMenu)
        {
            _loss = loss;
            _menus = menus;
            _lostMenu = lostMenu;
        }

        public void Tick()
        {
            if (_loss.IsLost) _menus.Request(new MenuRequest(MenuOwner, _lostMenu, MenuKind.Modal, pinned: true));
            else _menus.Withdraw(MenuOwner);
        }
    }
}
