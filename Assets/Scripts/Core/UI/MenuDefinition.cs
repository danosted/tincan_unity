#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Settings;
using UnityEngine;

namespace TinCan.Core.UI
{
    public enum MenuItemKind
    {
        Command,
        TextField,
        Toggle,
        Submenu,
        Back,
        /// <summary>Placeholder in a definition: expands to one <see cref="Binding"/> row per key the player can change.</summary>
        Bindings,
        /// <summary>Generated: one rebindable key (its value is the key); invoking it waits for a new key.</summary>
        Binding,
        /// <summary>Generated: a line of text (why the last rebind was refused).</summary>
        Note,
        /// <summary>
        /// Placeholder in a definition: expands to one row per player setting in its <see cref="MenuItemDefinition.SettingsGroup"/>
        /// (a <see cref="Slider"/>, <see cref="Toggle"/>, <see cref="Choice"/> or <see cref="TextField"/>), then a
        /// Reset to defaults row. The rows read and write <c>IPlayerSettings</c>.
        /// </summary>
        Settings,
        /// <summary>Generated: a number in a range (the row's Min, Max and Step).</summary>
        Slider,
        /// <summary>Generated: one of the row's Options; invoking it moves to the next one.</summary>
        Choice
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
        [Tooltip("Settings rows: which settings page this row expands to.")]
        public SettingGroup SettingsGroup;
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
