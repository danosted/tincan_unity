#nullable enable
using System.Collections.Generic;

namespace TinCan.Core.UI
{
    /// <summary>Immutable view model of the menu on top: its rows, and the request it belongs to.</summary>
    public sealed class MenuSnapshot
    {
        public string MenuId { get; }
        public string Title { get; }
        public IReadOnlyList<MenuItemRow> Items { get; }
        public bool CanGoBack { get; }
        public string Owner { get; }
        public MenuKind Kind { get; }
        public bool Pinned { get; }
        /// <summary>The row with keyboard and gamepad focus (an index into <see cref="Items"/>), -1 for none.</summary>
        public int Focused { get; }

        public MenuSnapshot(string menuId, string title, IReadOnlyList<MenuItemRow> items, bool canGoBack,
            string owner = "", MenuKind kind = MenuKind.Screen, bool pinned = false, int focused = -1)
        {
            MenuId = menuId;
            Title = title;
            Items = items;
            CanGoBack = canGoBack;
            Owner = owner;
            Kind = kind;
            Pinned = pinned;
            Focused = focused;
        }
    }
}
