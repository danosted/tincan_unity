#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>
    /// Everything a command may touch when a menu row is invoked. Values are those of the menu the row lives in.
    /// </summary>
    public readonly struct MenuContext
    {
        public readonly IMenuSystem Menus;
        /// <summary>The request the row belongs to.</summary>
        public readonly string Owner;
        public readonly string MenuId;
        public readonly string ItemId;

        /// <summary>A row of a menu opened with <see cref="IMenuSystem.Open"/>, whose owner is the menu's id.</summary>
        public MenuContext(IMenuSystem menus, string menuId, string itemId) : this(menus, menuId, menuId, itemId) { }

        public MenuContext(IMenuSystem menus, string owner, string menuId, string itemId)
        {
            Menus = menus;
            Owner = owner;
            MenuId = menuId;
            ItemId = itemId;
        }

        public string GetValue(string itemId) => Menus.GetValue(itemId);

        /// <summary>Closes the request this row belongs to (pinned or not), leaving other features' menus alone.</summary>
        public void Close() => Menus.Withdraw(Owner);
    }
}
