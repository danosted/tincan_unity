#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Immutable view model of one menu row; views render these without knowing the definition asset. A
    /// <see cref="MenuItemKind.Slider"/> row carries its range, a <see cref="MenuItemKind.Choice"/> row its options.
    /// </summary>
    public readonly struct MenuItemRow
    {
        public readonly string ItemId;
        public readonly string Label;
        public readonly MenuItemKind Kind;
        public readonly string Value;
        public readonly float Min;
        public readonly float Max;
        public readonly float Step;
        public readonly IReadOnlyList<string> Options;

        public MenuItemRow(string itemId, string label, MenuItemKind kind, string value)
            : this(itemId, label, kind, value, 0f, 0f, 0f, Array.Empty<string>()) { }

        public MenuItemRow(string itemId, string label, MenuItemKind kind, string value, float min, float max, float step, IReadOnlyList<string> options)
        {
            ItemId = itemId;
            Label = label;
            Kind = kind;
            Value = value;
            Min = min;
            Max = max;
            Step = step;
            Options = options;
        }
    }
}
