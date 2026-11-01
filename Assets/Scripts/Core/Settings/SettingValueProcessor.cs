#nullable enable
using System;
using System.Globalization;
using System.Linq;
using TinCan.Core.Domain.Settings;
using UnityEngine;

namespace TinCan.Core.Settings
{
    /// <summary>
    /// Checks a value against its definition and returns the value to keep: a slider is parsed, clamped to its range and
    /// snapped to its step; a toggle becomes True or False; a choice must be one of its options; text is cut to its
    /// length (not trimmed: a name is typed a letter at a time). Anything that cannot be read falls back to the
    /// definition's default. Values are text in the invariant culture, so a saved "1.5" reads the same on every machine.
    /// </summary>
    public static class SettingValueProcessor
    {
        public static string Normalize(SettingDefinition definition, string? value) => definition.Kind switch
        {
            SettingKind.Slider => Slider(definition, value),
            SettingKind.Toggle => TryBool(value, out bool flag) || TryBool(definition.Default, out flag) ? Format(flag) : bool.FalseString,
            SettingKind.Choice => Choice(definition, value),
            _ => Text(definition, value)
        };

        public static float ParseFloat(string value) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ? number : 0f;

        public static bool ParseBool(string value) => TryBool(value, out bool flag) && flag;

        public static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Format(bool value) => value ? bool.TrueString : bool.FalseString;

        private static string Slider(SettingDefinition definition, string? value)
        {
            float min = Mathf.Min(definition.Min, definition.Max);
            float max = Mathf.Max(definition.Min, definition.Max);
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || float.IsNaN(number) || float.IsInfinity(number))
            {
                if (!float.TryParse(definition.Default, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) number = min;
            }

            number = Mathf.Clamp(number, min, max);
            if (definition.Step > 0f) number = Mathf.Clamp(min + Mathf.Round((number - min) / definition.Step) * definition.Step, min, max);
            return Format(number);
        }

        private static string Choice(SettingDefinition definition, string? value)
        {
            if (value != null && definition.Options.Contains(value)) return value;
            if (definition.Options.Contains(definition.Default)) return definition.Default;
            return definition.Options.Count > 0 ? definition.Options[0] : string.Empty;
        }

        private static string Text(SettingDefinition definition, string? value)
        {
            string text = value ?? definition.Default;
            return definition.MaxLength > 0 && text.Length > definition.MaxLength ? text.Substring(0, definition.MaxLength) : text;
        }

        private static bool TryBool(string? value, out bool flag) => bool.TryParse(value, out flag);
    }
}
