#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.UI
{
    public enum MenuItemKind
    {
        Command,
        TextField,
        Toggle,
        Submenu,
        Back
    }

    /// <summary>
    /// One row in a data-driven menu. What a row does is decided by its Kind plus either a CommandId
    /// (resolved through <see cref="MenuCommandRegistry"/>) or a Submenu definition.
    /// </summary>
    [System.Serializable]
    public struct MenuItemDefinition
    {
        public string ItemId;
        public string Label;
        public MenuItemKind Kind;
        public string CommandId;
        public MenuDefinition? Submenu;
        public string DefaultValue;
    }

    /// <summary>
    /// Data-driven menu structure. Authored as an asset; rendered by whatever view is bound to <see cref="IMenuSystem"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Menu_New", menuName = "TinCan/UI/Menu Definition")]
    public class MenuDefinition : ScriptableObject
    {
        [SerializeField] private string _menuId = string.Empty;
        [SerializeField] private string _title = string.Empty;
        [SerializeField] private List<MenuItemDefinition> _items = new();

        public string MenuId => string.IsNullOrEmpty(_menuId) ? name : _menuId;
        public string Title => _title;
        public IReadOnlyList<MenuItemDefinition> Items => _items;

        /// <summary>Test and tooling helper: build a definition in code without an asset file.</summary>
        public static MenuDefinition Create(string menuId, string title, params MenuItemDefinition[] items)
        {
            var definition = CreateInstance<MenuDefinition>();
            definition._menuId = menuId;
            definition._title = title;
            definition._items = new List<MenuItemDefinition>(items);
            return definition;
        }
    }
}
