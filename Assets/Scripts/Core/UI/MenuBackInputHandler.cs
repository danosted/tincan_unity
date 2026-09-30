#nullable enable
using TinCan.Core.Domain.Input;

namespace TinCan.Core.UI
{
    /// <summary>Cancel while a menu is open: step back (the last Back closes the menu).</summary>
    public sealed class MenuBackInputHandler : InputCommandHandler<MenuBackCommand>
    {
        private readonly IMenuSystem _menus;

        public MenuBackInputHandler(IMenuSystem menus)
        {
            _menus = menus;
        }

        protected override bool Handle(MenuBackCommand command)
        {
            if (!_menus.IsOpen) return false;
            _menus.Back();
            return true;
        }
    }
}
