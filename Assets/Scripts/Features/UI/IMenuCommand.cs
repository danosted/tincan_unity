#nullable enable
namespace TinCan.Features.UI
{
    /// <summary>
    /// Everything a command may touch when a menu row is invoked. Values are those of the menu the row lives in.
    /// </summary>
    public readonly struct MenuContext
    {
        public readonly IMenuSystem Menus;
        public readonly string MenuId;
        public readonly string ItemId;

        public MenuContext(IMenuSystem menus, string menuId, string itemId)
        {
            Menus = menus;
            MenuId = menuId;
            ItemId = itemId;
        }

        public string GetValue(string itemId) => Menus.GetValue(itemId);
    }

    /// <summary>
    /// A headless menu action. Register implementations with <c>.As&lt;IMenuCommand&gt;()</c>;
    /// <see cref="MenuCommandRegistry"/> collects them and <see cref="MenuUseCase"/> dispatches by CommandId.
    /// </summary>
    public interface IMenuCommand
    {
        string CommandId { get; }
        void Execute(MenuContext context);
    }
}
