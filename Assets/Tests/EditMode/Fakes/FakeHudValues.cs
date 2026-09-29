#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.UI;
using TinCan.Core.Domain.Hud;

namespace TinCan.Tests.EditMode.Fakes
{
    public class FakeHudValues : IHudValues
    {
        private readonly Dictionary<string, string> _values = new();
        private readonly Dictionary<string, float> _meters = new();

        public IReadOnlyDictionary<string, string> All => _values;
        public IReadOnlyDictionary<string, float> Meters => _meters;
        public event Action? Changed;
        public int SetCalls { get; private set; }

        public void Set(string key, string text)
        {
            SetCalls++;
            _values[key] = text;
            Changed?.Invoke();
        }

        public void Remove(string key)
        {
            if (_values.Remove(key)) Changed?.Invoke();
        }

        public void SetMeter(string key, float fill)
        {
            _meters[key] = fill;
            Changed?.Invoke();
        }

        public void RemoveMeter(string key)
        {
            if (_meters.Remove(key)) Changed?.Invoke();
        }
    }
}
