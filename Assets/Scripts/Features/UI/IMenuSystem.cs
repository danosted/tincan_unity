#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.UI
{
    /// <summary>Immutable view model of one menu row; views render these without knowing the definition asset.</summary>
    public readonly struct MenuItemRow
    {
        public readonly string ItemId;
        public readonly string Label;
        public readonly MenuItemKind Kind;
        public readonly string Value;

        public MenuItemRow(string itemId, string label, MenuItemKind kind, string value)
        {
            ItemId = itemId;
            Label = label;
            Kind = kind;
            Value = value;
        }
    }

    /// <summary>Immutable view model of the currently visible menu.</summary>
    public sealed class MenuSnapshot
    {
        public string MenuId { get; }
        public string Title { get; }
        public IReadOnlyList<MenuItemRow> Items { get; }
        public bool CanGoBack { get; }

        public MenuSnapshot(string menuId, string title, IReadOnlyList<MenuItemRow> items, bool canGoBack)
        {
            MenuId = menuId;
            Title = title;
            Items = items;
            CanGoBack = canGoBack;
        }
    }

    /// <summary>
    /// Headless menu API. Views subscribe to <see cref="Changed"/> and render <see cref="Current"/>;
    /// gameplay code never talks to a view directly.
    /// </summary>
    public interface IMenuSystem
    {
        MenuSnapshot? Current { get; }
        bool IsOpen { get; }
        event Action? Changed;

        void Open(MenuDefinition menu);
        void Back();
        void CloseAll();
        void Invoke(string itemId);
        void SetValue(string itemId, string value);
        string GetValue(string itemId);
    }
}
