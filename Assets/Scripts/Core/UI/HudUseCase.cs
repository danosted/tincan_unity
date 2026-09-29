#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Hud;
using UnityEngine;

namespace TinCan.Core.UI
{
    public class HudUseCase : IHudValues
    {
        // Presenters set meters every frame; changes smaller than a tenth of a percent do not redraw the HUD.
        private const float MeterEpsilon = 0.001f;

        private readonly Dictionary<string, string> _values = new();
        private readonly Dictionary<string, float> _meters = new();

        public IReadOnlyDictionary<string, string> All => _values;
        public IReadOnlyDictionary<string, float> Meters => _meters;
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

        public void SetMeter(string key, float fill)
        {
            if (string.IsNullOrEmpty(key)) return;
            fill = Mathf.Clamp01(fill);
            if (_meters.TryGetValue(key, out var existing) && Mathf.Abs(existing - fill) < MeterEpsilon) return;

            _meters[key] = fill;
            Changed?.Invoke();
        }

        public void RemoveMeter(string key)
        {
            if (!_meters.Remove(key)) return;
            Changed?.Invoke();
        }
    }
}
