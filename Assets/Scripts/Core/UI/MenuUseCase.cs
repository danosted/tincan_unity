#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Settings;
using VContainer;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Application Layer: owns the open menu requests (one per owner, each with its own stack of submenus), the per-menu
    /// values, and dispatches commands. The request on top is chosen by <see cref="MenuArbitrationProcessor"/>; rows,
    /// values and Back act on it alone, so Back never falls into another feature's menu. Cancel reaches it through the
    /// input contexts (MenuBackInputHandler, OpenMenuInputHandler). A <see cref="MenuItemKind.Bindings"/> row expands into
    /// one row per key the player can change (<see cref="IInputBindings"/>); invoking one waits for the new key. A
    /// <see cref="MenuItemKind.Settings"/> row expands into the player settings of its group (<see cref="MenuSettingsRows"/>).
    /// Pure C# apart from the ScriptableObject definitions it is handed.
    /// </summary>
    public class MenuUseCase : IMenuSystem
    {
        public const string WaitingForKey = "Press a key... (Esc cancels)";

        private readonly IMenuCommandRegistry _commands;
        private readonly IInputBindings? _bindings;
        private readonly List<OpenRequest> _requests = new();
        private readonly List<MenuRequestRank> _ranks = new();
        private readonly Dictionary<string, string> _values = new();
        private readonly Dictionary<string, InputBindingSlot> _bindingRows = new();
        private readonly MenuSettingsRows? _settingRows;
        private MenuSnapshot? _current;
        private int _sequence;
        private OpenRequest? _focusedRequest;
        private MenuDefinition? _focusedMenu;
        private string? _focusedItem;

        public MenuUseCase(IMenuCommandRegistry commands) : this(commands, null, null) { }

        public MenuUseCase(IMenuCommandRegistry commands, IInputBindings? bindings) : this(commands, bindings, null) { }

        [Inject]
        public MenuUseCase(IMenuCommandRegistry commands, IInputBindings? bindings, IPlayerSettings? settings)
        {
            _commands = commands;
            _bindings = bindings;
            if (_bindings != null) _bindings.Changed += OnBindingsChanged;
            if (settings == null) return;
            _settingRows = new MenuSettingsRows(settings);
            settings.Changed += _ => OnBindingsChanged();
        }

        public MenuSnapshot? Current => _current;
        public bool IsOpen => _requests.Count > 0;
        public event Action? Changed;

        public void Request(MenuRequest request)
        {
            if (request.Menu == null || string.IsNullOrEmpty(request.Owner)) return;

            int index = IndexOf(request.Owner);
            if (index >= 0)
            {
                var open = _requests[index];
                if (open.Stack[0].MenuId == request.Menu.MenuId && open.Kind == request.Kind && open.Pinned == request.Pinned) return;
                Remove(index);
            }

            SeedValues(request.Menu);
            _requests.Add(new OpenRequest(request.Owner, request.Kind, request.Pinned, ++_sequence, request.Menu));
            Rebuild();
        }

        public void Withdraw(string owner)
        {
            int index = IndexOf(owner);
            if (index < 0) return;
            Remove(index);
            Rebuild();
        }

        public bool IsRequested(string owner) => IndexOf(owner) >= 0;

        public void Open(MenuDefinition menu)
        {
            if (menu == null) return;
            Request(new MenuRequest(menu.MenuId, menu, MenuKind.Screen));
        }

        public bool Back()
        {
            var top = Top();
            if (top == null) return false;

            if (top.Stack.Count > 1)
            {
                _bindings?.CancelRebind();
                top.Stack.RemoveAt(top.Stack.Count - 1);
                Rebuild();
                return true;
            }

            if (top.Pinned) return false;
            Withdraw(top.Owner);
            return true;
        }

        public void CloseAll()
        {
            int before = _requests.Count;
            for (int i = _requests.Count - 1; i >= 0; i--)
            {
                if (!_requests[i].Pinned) Remove(i);
            }
            if (_requests.Count != before) Rebuild();
        }

        public void Invoke(string itemId)
        {
            if (IsOpen && _bindingRows.TryGetValue(itemId, out var slot))
            {
                _bindings!.StartRebind(slot);
                return;
            }

            if (IsOpen && _settingRows != null && _settingRows.TryInvoke(itemId)) return;
            if (!TryGetItem(itemId, out var request, out var menu, out var item)) return;

            switch (item.Kind)
            {
                case MenuItemKind.Command when _commands.TryGetCommand(item.CommandId, out var command):
                    command.Execute(new MenuContext(this, request.Owner, menu.MenuId, item.ItemId));
                    break;
                case MenuItemKind.Submenu when item.Submenu != null:
                    SeedValues(item.Submenu);
                    request.Stack.Add(item.Submenu);
                    Rebuild();
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
            if (IsOpen && _settingRows != null && _settingRows.TrySetValue(itemId, value)) return;
            if (!TryGetItem(itemId, out _, out var menu, out _)) return;

            var key = ValueKey(menu.MenuId, itemId);
            if (_values.TryGetValue(key, out var existing) && existing == value) return;

            _values[key] = value;
            Rebuild();
        }

        public void Focus(string itemId)
        {
            if (_current == null) return;
            for (int i = 0; i < _current.Items.Count; i++)
            {
                if (_current.Items[i].ItemId != itemId || !MenuFocusProcessor.Focusable(_current.Items[i].Kind)) continue;
                if (_focusedItem == itemId) return;
                _focusedItem = itemId;
                Rebuild();
                return;
            }
        }

        public void MoveFocus(int delta)
        {
            if (_current == null) return;
            int next = MenuFocusProcessor.Move(_current.Items, _current.Focused, delta);
            if (next < 0 || next == _current.Focused) return;
            _focusedItem = _current.Items[next].ItemId;
            Rebuild();
        }

        public void Adjust(int direction)
        {
            if (_current is not { Focused: >= 0 } snapshot) return;
            var row = snapshot.Items[snapshot.Focused];
            if (row.Kind == MenuItemKind.Toggle)
            {
                Invoke(row.ItemId);
                return;
            }

            if (MenuFocusProcessor.Adjust(row, direction) is { } value) SetValue(row.ItemId, value);
        }

        public void Submit()
        {
            if (_current is { Focused: >= 0 } snapshot) Invoke(snapshot.Items[snapshot.Focused].ItemId);
        }

        /// <summary>
        /// The focused row of the menu being built: kept by item id while the same menu stays on top, the first focusable
        /// row when another menu comes up (or the focused row is gone).
        /// </summary>
        private int FocusIn(OpenRequest top, MenuDefinition menu, IReadOnlyList<MenuItemRow> rows)
        {
            if (!ReferenceEquals(_focusedRequest, top) || !ReferenceEquals(_focusedMenu, menu)) _focusedItem = null;
            _focusedRequest = top;
            _focusedMenu = menu;

            int index = -1;
            for (int i = 0; i < rows.Count && _focusedItem != null; i++)
            {
                if (rows[i].ItemId == _focusedItem) index = i;
            }

            if (index < 0) index = MenuFocusProcessor.First(rows);
            _focusedItem = index < 0 ? null : rows[index].ItemId;
            return index;
        }

        public string GetValue(string itemId)
        {
            if (IsOpen && _settingRows != null && _settingRows.TryGetValue(itemId, out var setting)) return setting;
            if (!TryGetItem(itemId, out _, out var menu, out _)) return string.Empty;
            return _values.TryGetValue(ValueKey(menu.MenuId, itemId), out var value) ? value : string.Empty;
        }

        private OpenRequest? Top()
        {
            _ranks.Clear();
            foreach (var request in _requests) _ranks.Add(new MenuRequestRank(request.Kind, request.Sequence));
            int top = MenuArbitrationProcessor.Top(_ranks);
            return top < 0 ? null : _requests[top];
        }

        private int IndexOf(string owner) => _requests.FindIndex(r => r.Owner == owner);

        private void Remove(int index)
        {
            if (ReferenceEquals(_requests[index], Top())) _bindings?.CancelRebind();
            _requests.RemoveAt(index);
        }

        private void SeedValues(MenuDefinition menu)
        {
            foreach (var item in menu.Items)
            {
                var key = ValueKey(menu.MenuId, item.ItemId);
                if (!_values.ContainsKey(key)) _values[key] = item.DefaultValue ?? string.Empty;
            }
        }

        private bool TryGetItem(string itemId, out OpenRequest request, out MenuDefinition menu, out MenuItemDefinition item)
        {
            request = null!;
            menu = null!;
            item = default;
            var top = Top();
            if (top == null) return false;

            request = top;
            menu = top.Stack[top.Stack.Count - 1];
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
            var top = Top();
            _bindingRows.Clear();
            _settingRows?.Clear();
            if (top == null)
            {
                _current = null;
                Changed?.Invoke();
                return;
            }

            var menu = top.Stack[top.Stack.Count - 1];
            var rows = new List<MenuItemRow>(menu.Items.Count);
            foreach (var item in menu.Items)
            {
                if (item.Kind == MenuItemKind.Bindings)
                {
                    AddBindingRows(item, rows);
                    continue;
                }

                if (item.Kind == MenuItemKind.Settings)
                {
                    _settingRows?.Add(item, rows);
                    continue;
                }

                _values.TryGetValue(ValueKey(menu.MenuId, item.ItemId), out var value);
                if (item.Kind == MenuItemKind.Note)
                {
                    AddNoteRow(item, value, rows);
                    continue;
                }

                rows.Add(new MenuItemRow(item.ItemId, item.Label, item.Kind, value ?? string.Empty));
            }

            _current = new MenuSnapshot(menu.MenuId, menu.Title, rows, top.Stack.Count > 1, top.Owner, top.Kind, top.Pinned, FocusIn(top, menu, rows));
            Changed?.Invoke();
        }

        /// <summary>
        /// An authored note shows its value when a command set one (the Join menu's status line), else its label; with
        /// neither it is left out, so a status line takes no room until there is something to say.
        /// </summary>
        private static void AddNoteRow(MenuItemDefinition item, string? value, List<MenuItemRow> rows)
        {
            string text = string.IsNullOrEmpty(value) ? item.Label : value!;
            if (string.IsNullOrEmpty(text)) return;
            rows.Add(new MenuItemRow(item.ItemId, text, MenuItemKind.Note, string.Empty));
        }

        private void AddBindingRows(MenuItemDefinition item, List<MenuItemRow> rows)
        {
            if (_bindings == null) return;

            for (int i = 0; i < _bindings.Slots.Count; i++)
            {
                var slot = _bindings.Slots[i];
                string rowId = $"{item.ItemId}/{i}";
                bool waiting = _bindings.Rebinding is { } rebinding && rebinding.SameAs(slot);
                rows.Add(new MenuItemRow(rowId, $"{slot.Group}: {slot.Label}", MenuItemKind.Binding, waiting ? WaitingForKey : _bindings.Describe(slot)));
                _bindingRows[rowId] = slot;
            }

            if (_bindings.LastMessage is { } message) rows.Add(new MenuItemRow(item.ItemId + "/note", message, MenuItemKind.Note, string.Empty));
        }

        private void OnBindingsChanged()
        {
            if (IsOpen) Rebuild();
        }

        private static string ValueKey(string menuId, string itemId) => menuId + "/" + itemId;

        /// <summary>One owner's request and its stack of submenus (the root first).</summary>
        private sealed class OpenRequest
        {
            public readonly string Owner;
            public readonly MenuKind Kind;
            public readonly bool Pinned;
            public readonly int Sequence;
            public readonly List<MenuDefinition> Stack;

            public OpenRequest(string owner, MenuKind kind, bool pinned, int sequence, MenuDefinition root)
            {
                Owner = owner;
                Kind = kind;
                Pinned = pinned;
                Sequence = sequence;
                Stack = new List<MenuDefinition> { root };
            }
        }
    }
}
