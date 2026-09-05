#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.UI
{
    /// <summary>
    /// Headless HUD: named text values that any view can render. Deliberately tiny; grow it when a real need appears.
    /// </summary>
    public interface IHudValues
    {
        IReadOnlyDictionary<string, string> All { get; }
        event Action? Changed;

        void Set(string key, string text);
        void Remove(string key);
    }
}
