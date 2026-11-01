#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Settings;

namespace TinCan.Core.UI
{
    /// <summary>
    /// The rows a <see cref="MenuItemKind.Settings"/> placeholder expands to, for <see cref="MenuUseCase"/>: one per player
    /// setting in the row's group (slider, toggle, choice or text field), then Reset to defaults. Row ids are
    /// <c>{placeholder}/{setting key}</c>; values go straight to <see cref="IPlayerSettings"/>, not to the menu's own
    /// values. A group with no setting in this scene shows a note instead.
    /// </summary>
    public sealed class MenuSettingsRows
    {
        public const string ResetLabel = "Reset to defaults";
        public const string EmptyLabel = "Nothing to set here in this scene.";

        private readonly IPlayerSettings _settings;
        private readonly Dictionary<string, SettingDefinition> _rows = new();
        private readonly Dictionary<string, SettingGroup> _resets = new();

        public MenuSettingsRows(IPlayerSettings settings)
        {
            _settings = settings;
        }

        public void Clear()
        {
            _rows.Clear();
            _resets.Clear();
        }

        public void Add(MenuItemDefinition placeholder, List<MenuItemRow> rows)
        {
            int before = rows.Count;
            foreach (var definition in _settings.Definitions)
            {
                if (definition.Group != placeholder.SettingsGroup) continue;

                string rowId = $"{placeholder.ItemId}/{definition.Key}";
                _rows[rowId] = definition;
                rows.Add(Row(rowId, definition, _settings.GetString(definition.Key)));
            }

            if (rows.Count == before)
            {
                rows.Add(new MenuItemRow(placeholder.ItemId + "/empty", EmptyLabel, MenuItemKind.Note, string.Empty));
                return;
            }

            string resetId = placeholder.ItemId + "/reset";
            _resets[resetId] = placeholder.SettingsGroup;
            rows.Add(new MenuItemRow(resetId, ResetLabel, MenuItemKind.Command, string.Empty));
        }

        /// <summary>A toggle flips, a choice moves to its next option, Reset resets the page. False when the row is not ours.</summary>
        public bool TryInvoke(string rowId)
        {
            if (_resets.TryGetValue(rowId, out var group))
            {
                _settings.ResetGroup(group);
                return true;
            }

            if (!_rows.TryGetValue(rowId, out var definition)) return false;

            string value = _settings.GetString(definition.Key);
            switch (definition.Kind)
            {
                case SettingKind.Toggle:
                    _settings.Set(definition.Key, value == bool.TrueString ? bool.FalseString : bool.TrueString);
                    break;
                case SettingKind.Choice when definition.Options.Count > 0:
                    int index = Math.Max(0, IndexOf(definition.Options, value));
                    _settings.Set(definition.Key, definition.Options[(index + 1) % definition.Options.Count]);
                    break;
            }
            return true;
        }

        public bool TrySetValue(string rowId, string value)
        {
            if (!_rows.TryGetValue(rowId, out var definition)) return false;
            _settings.Set(definition.Key, value);
            return true;
        }

        public bool TryGetValue(string rowId, out string value)
        {
            value = _rows.TryGetValue(rowId, out var definition) ? _settings.GetString(definition.Key) : string.Empty;
            return _rows.ContainsKey(rowId);
        }

        private static MenuItemRow Row(string rowId, SettingDefinition definition, string value) => definition.Kind switch
        {
            SettingKind.Slider => new MenuItemRow(rowId, definition.Label, MenuItemKind.Slider, value, definition.Min, definition.Max, definition.Step, Array.Empty<string>()),
            SettingKind.Toggle => new MenuItemRow(rowId, definition.Label, MenuItemKind.Toggle, value),
            SettingKind.Choice => new MenuItemRow(rowId, definition.Label, MenuItemKind.Choice, value, 0f, 0f, 0f, definition.Options),
            _ => new MenuItemRow(rowId, definition.Label, MenuItemKind.TextField, value)
        };

        private static int IndexOf(IReadOnlyList<string> options, string value)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i], value, StringComparison.Ordinal)) return i;
            }
            return -1;
        }
    }
}
