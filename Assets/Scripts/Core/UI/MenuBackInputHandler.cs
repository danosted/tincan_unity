#nullable enable
using TinCan.Core.Domain.Input;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Cancel while a menu is open: step back inside the menu on top (the last Back closes it). When that menu is pinned
    /// (the overboard choice, the end screen), the pause menu opens over it instead, so Quit is always reachable; closing
    /// the pause menu returns to the pinned one.
    /// </summary>
    public sealed class MenuBackInputHandler : InputCommandHandler<MenuBackCommand>
    {
        private readonly IMenuSystem _menus;
        private readonly MenuDefinition _pauseMenu;

        public MenuBackInputHandler(IMenuSystem menus, MenuDefinition pauseMenu)
        {
            _menus = menus;
            _pauseMenu = pauseMenu;
        }

        protected override bool Handle(MenuBackCommand command)
        {
            if (!_menus.IsOpen) return false;
            if (_menus.Back()) return true;

            // A modal request, so it beats the pinned modal underneath as the newest of its kind.
            _menus.Request(new MenuRequest(MenuOwners.Main, _pauseMenu, MenuKind.Modal));
            return true;
        }
    }
}
