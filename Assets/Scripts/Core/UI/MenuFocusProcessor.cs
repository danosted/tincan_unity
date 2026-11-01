#nullable enable
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Keyboard and gamepad focus in a menu, pure: which rows take focus, where up and down go, and what left and right do
    /// to a row's value. A note never takes focus; the focus wraps at both ends.
    /// </summary>
    public static class MenuFocusProcessor
    {
        public static bool Focusable(MenuItemKind kind) => kind != MenuItemKind.Note;

        /// <summary>The first focusable row, or -1.</summary>
        public static int First(IReadOnlyList<MenuItemRow> rows) => Move(rows, -1, 1);

        /// <summary>The next focusable row <paramref name="delta"/> steps (+1 down, -1 up) from <paramref name="from"/>, wrapping; -1 when none.</summary>
        public static int Move(IReadOnlyList<MenuItemRow> rows, int from, int delta)
        {
            if (rows.Count == 0 || delta == 0) return from;
            int step = delta > 0 ? 1 : -1;
            int index = from;
            for (int tried = 0; tried < rows.Count; tried++)
            {
                index = ((index + step) % rows.Count + rows.Count) % rows.Count;
                if (Focusable(rows[index].Kind)) return index;
            }
            return -1;
        }

        /// <summary>
        /// The value left (-1) or right (+1) gives a row: a slider moves one step (a twentieth of its range without one),
        /// a choice moves to the previous or next option (wrapping), a toggle flips. Null for rows left and right do not
        /// change.
        /// </summary>
        public static string? Adjust(MenuItemRow row, int direction) => row.Kind switch
        {
            MenuItemKind.Slider => Slide(row, direction),
            MenuItemKind.Choice => Step(row.Options, row.Value, direction),
            MenuItemKind.Toggle => row.Value == bool.TrueString ? bool.FalseString : bool.TrueString,
            _ => null
        };

        /// <summary>The option <paramref name="direction"/> places from the current one, wrapping around.</summary>
        public static string Step(IReadOnlyList<string> options, string current, int direction)
        {
            if (options.Count == 0) return current;
            int index = 0;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] == current) index = i;
            }
            return options[((index + direction) % options.Count + options.Count) % options.Count];
        }

        private static string Slide(MenuItemRow row, int direction)
        {
            float step = row.Step > 0f ? row.Step : Mathf.Abs(row.Max - row.Min) / 20f;
            float value = float.TryParse(row.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : row.Min;
            float next = Mathf.Clamp(value + step * direction, Mathf.Min(row.Min, row.Max), Mathf.Max(row.Min, row.Max));
            return next.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
