#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Domain.Settings
{
    /// <summary>
    /// This player's settings on this machine, keyed by <see cref="SettingDefinition.Key"/>. Values are checked against
    /// their definition when set. Reading a key no loaded definition has logs once and returns the type's default.
    /// </summary>
    public interface IPlayerSettings
    {
        /// <summary>Every setting this scene has: the core ones plus those the loaded features contribute.</summary>
        IReadOnlyList<SettingDefinition> Definitions { get; }

        /// <summary>Raised with the key after a value changes.</summary>
        event Action<string>? Changed;

        /// <summary>Whether a loaded definition has this key.</summary>
        bool Has(string key);

        float GetFloat(string key);
        bool GetBool(string key);
        string GetString(string key);

        /// <summary>Sets a value, clamped and checked against the definition (a slider snaps to its step).</summary>
        void Set(string key, string value);
        void ResetGroup(SettingGroup group);
    }
}
