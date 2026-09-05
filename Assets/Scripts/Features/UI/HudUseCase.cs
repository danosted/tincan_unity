#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.UI
{
    public class HudUseCase : IHudValues
    {
        private readonly Dictionary<string, string> _values = new();

        public IReadOnlyDictionary<string, string> All => _values;
        public event Action? Changed;

        public void Set(string key, string text)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_values.TryGetValue(key, out var existing) && existing == text) return;

            _values[key] = text;
            Changed?.Invoke();
        }

        public void Remove(string key)
        {
            if (!_values.Remove(key)) return;
            Changed?.Invoke();
        }
    }
}
