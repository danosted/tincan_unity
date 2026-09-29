#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Domain.Hud
{
    /// <summary>
    /// Headless HUD: named text values and named meters (a fill from 0 to 1) that any view can render. Deliberately
    /// tiny; grow it when a real need appears.
    /// </summary>
    public interface IHudValues
    {
        IReadOnlyDictionary<string, string> All { get; }

        /// <summary>Meters by label, each a fill in [0, 1] (the ship's health, a reload).</summary>
        IReadOnlyDictionary<string, float> Meters { get; }

        event Action? Changed;

        void Set(string key, string text);
        void Remove(string key);

        /// <summary>Shows or updates a meter; the fill is clamped to [0, 1].</summary>
        void SetMeter(string key, float fill);
        void RemoveMeter(string key);
    }
}
