#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Settings;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>String settings set by the test; a key is defined once it has a value.</summary>
    public sealed class FakePlayerSettings : IPlayerSettings
    {
        private readonly Dictionary<string, string> _values = new();

        public IReadOnlyList<SettingDefinition> Definitions => Array.Empty<SettingDefinition>();
        public event Action<string>? Changed;

        public bool Has(string key) => _values.ContainsKey(key);
        public float GetFloat(string key) => _values.TryGetValue(key, out var v) && float.TryParse(v, out var f) ? f : 0f;
        public bool GetBool(string key) => _values.TryGetValue(key, out var v) && v == "true";
        public string GetString(string key) => _values.TryGetValue(key, out var v) ? v : string.Empty;

        public void Set(string key, string value)
        {
            _values[key] = value;
            Changed?.Invoke(key);
        }

        public void ResetGroup(SettingGroup group) => _values.Clear();
    }
}
