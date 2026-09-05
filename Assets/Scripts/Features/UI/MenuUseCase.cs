#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.UI
{
    /// <summary>
    /// Application Layer: owns the menu stack and per-menu values and dispatches commands. Input is handled by MainMenuBootstrap.
    /// Pure C# apart from the ScriptableObject definitions it is handed.
    /// </summary>
    public class MenuUseCase : IMenuSystem
    {
        private readonly IMenuCommandRegistry _commands;
        private readonly List<MenuDefinition> _stack = new();
        private readonly Dictionary<string, string> _values = new();
        private MenuSnapshot? _current;

        public MenuUseCase(IMenuCommandRegistry commands)
        {
            _commands = commands;
        }

        public MenuSnapshot? Current => _current;
        public bool IsOpen => _stack.Count > 0;
        public event Action? Changed;

        public void Open(MenuDefinition menu)
        {
            if (menu == null) return;

            foreach (var item in menu.Items)
            {
                var key = ValueKey(menu.MenuId, item.ItemId);
                if (!_values.ContainsKey(key)) _values[key] = item.DefaultValue ?? string.Empty;
            }

            _stack.Add(menu);
            Rebuild();
        }

        public void Back()
        {
            if (!IsOpen) return;
            _stack.RemoveAt(_stack.Count - 1);
            Rebuild();
        }

        public void CloseAll()
        {
            if (!IsOpen) return;
            _stack.Clear();
            Rebuild();
        }

        public void Invoke(string itemId)
        {
            if (!TryGetItem(itemId, out var menu, out var item)) return;

            switch (item.Kind)
            {
                case MenuItemKind.Command when _commands.TryGetCommand(item.CommandId, out var command):
                    command.Execute(new MenuContext(this, menu.MenuId, item.ItemId));
                    break;
                case MenuItemKind.Submenu when item.Submenu != null:
                    Open(item.Submenu);
                    break;
                case MenuItemKind.Back:
                    Back();
                    break;
                case MenuItemKind.Toggle:
                    SetValue(itemId, GetValue(itemId) == bool.TrueString ? bool.FalseString : bool.TrueString);
                    break;
            }
        }

        public void SetValue(string itemId, string value)
        {
            if (!TryGetItem(itemId, out var menu, out _)) return;

            var key = ValueKey(menu.MenuId, itemId);
            if (_values.TryGetValue(key, out var existing) && existing == value) return;

            _values[key] = value;
            Rebuild();
        }

        public string GetValue(string itemId)
        {
            if (!TryGetItem(itemId, out var menu, out _)) return string.Empty;
            return _values.TryGetValue(ValueKey(menu.MenuId, itemId), out var value) ? value : string.Empty;
        }

        private bool TryGetItem(string itemId, out MenuDefinition menu, out MenuItemDefinition item)
        {
            menu = null!;
            item = default;
            if (!IsOpen) return false;

            menu = _stack[_stack.Count - 1];
            foreach (var candidate in menu.Items)
            {
                if (candidate.ItemId != itemId) continue;
                item = candidate;
                return true;
            }
            return false;
        }

        private void Rebuild()
        {
            if (!IsOpen)
            {
                _current = null;
                Changed?.Invoke();
                return;
            }

            var menu = _stack[_stack.Count - 1];
            var rows = new List<MenuItemRow>(menu.Items.Count);
            foreach (var item in menu.Items)
            {
                _values.TryGetValue(ValueKey(menu.MenuId, item.ItemId), out var value);
                rows.Add(new MenuItemRow(item.ItemId, item.Label, item.Kind, value ?? string.Empty));
            }

            _current = new MenuSnapshot(menu.MenuId, menu.Title, rows, _stack.Count > 1);
            Changed?.Invoke();
        }

        private static string ValueKey(string menuId, string itemId) => menuId + "/" + itemId;
    }
}
