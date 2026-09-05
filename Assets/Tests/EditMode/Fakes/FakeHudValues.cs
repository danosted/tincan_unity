#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Features.UI;

namespace TinCan.Tests.EditMode.Fakes
{
    public class FakeHudValues : IHudValues
    {
        private readonly Dictionary<string, string> _values = new();

        public IReadOnlyDictionary<string, string> All => _values;
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
    }
}
