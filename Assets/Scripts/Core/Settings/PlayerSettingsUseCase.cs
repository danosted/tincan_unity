#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Settings;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Core.Settings
{
    /// <summary>
    /// <see cref="IPlayerSettings"/>: loads the saved values on start, checks every value against its definition
    /// (<see cref="SettingValueProcessor"/>), and saves a moment after the last change (a dragged slider saves once) and
    /// when the scope ends. Only values that differ from their default are saved; a saved value whose feature is not
    /// loaded in this scene is kept as it is.
    /// </summary>
    public sealed class PlayerSettingsUseCase : IPlayerSettings, ITickable, IDisposable
    {
        public const float SaveDelaySeconds = 0.5f;

        private readonly ISettingsStore _store;
        private readonly ITimeService _time;
        private readonly List<SettingDefinition> _definitions = new();
        private readonly Dictionary<string, SettingDefinition> _byKey = new();
        private readonly Dictionary<string, string> _values = new();
        private readonly Dictionary<string, string> _unloaded = new();
        private readonly HashSet<string> _warned = new();
        private float? _dirtySince;

        public PlayerSettingsUseCase(SettingsCatalog catalog, ISettingsStore store, ITimeService time)
        {
            _store = store;
            _time = time;
            foreach (var definition in catalog.All)
            {
                if (definition == null || string.IsNullOrEmpty(definition.Key) || _byKey.ContainsKey(definition.Key)) continue;
                _definitions.Add(definition);
                _byKey[definition.Key] = definition;
            }

            Load();
        }

        public IReadOnlyList<SettingDefinition> Definitions => _definitions;
        public event Action<string>? Changed;

        public bool Has(string key) => _byKey.ContainsKey(key);

        public float GetFloat(string key) => TryGet(key, out string value) ? SettingValueProcessor.ParseFloat(value) : 0f;

        public bool GetBool(string key) => TryGet(key, out string value) && SettingValueProcessor.ParseBool(value);

        public string GetString(string key) => TryGet(key, out string value) ? value : string.Empty;

        public void Set(string key, string value)
        {
            if (!_byKey.TryGetValue(key, out var definition))
            {
                Warn(key);
                return;
            }

            string normalized = SettingValueProcessor.Normalize(definition, value);
            if (Current(definition) == normalized) return;

            _values[key] = normalized;
            _dirtySince = _time.Time;
            Changed?.Invoke(key);
        }

        public void ResetGroup(SettingGroup group)
        {
            foreach (var definition in _definitions)
            {
                if (definition.Group == group) Set(definition.Key, definition.Default);
            }
        }

        public void Tick()
        {
            if (_dirtySince is not { } since || _time.Time - since < SaveDelaySeconds) return;
            Save();
        }

        public void Dispose()
        {
            if (_dirtySince != null) Save();
        }

        private bool TryGet(string key, out string value)
        {
            if (_byKey.TryGetValue(key, out var definition))
            {
                value = Current(definition);
                return true;
            }

            Warn(key);
            value = string.Empty;
            return false;
        }

        private string Current(SettingDefinition definition) =>
            _values.TryGetValue(definition.Key, out string value) ? value : SettingValueProcessor.Normalize(definition, definition.Default);

        private void Load()
        {
            string? json = _store.Load();
            if (string.IsNullOrEmpty(json)) return;

            SavedSettings? saved;
            try
            {
                saved = JsonUtility.FromJson<SavedSettings>(json);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("[PlayerSettings] The saved settings could not be read; using the defaults.");
                return;
            }

            foreach (var entry in saved?.Values ?? new List<SavedValue>())
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                if (_byKey.TryGetValue(entry.Key, out var definition)) _values[entry.Key] = SettingValueProcessor.Normalize(definition, entry.Value);
                else _unloaded[entry.Key] = entry.Value;
            }
        }

        private void Save()
        {
            _dirtySince = null;
            var saved = new SavedSettings();
            foreach (var pair in _unloaded) saved.Values.Add(new SavedValue { Key = pair.Key, Value = pair.Value });
            foreach (var definition in _definitions)
            {
                string value = Current(definition);
                if (value != SettingValueProcessor.Normalize(definition, definition.Default)) saved.Values.Add(new SavedValue { Key = definition.Key, Value = value });
            }

            _store.Save(JsonUtility.ToJson(saved));
        }

        private void Warn(string key)
        {
            if (_warned.Add(key)) Debug.LogWarning($"[PlayerSettings] No setting '{key}' is loaded in this scene; using the default.");
        }

        [Serializable]
        private sealed class SavedSettings
        {
            public List<SavedValue> Values = new();
        }

        [Serializable]
        private struct SavedValue
        {
            public string Key;
            public string Value;
        }
    }
}
