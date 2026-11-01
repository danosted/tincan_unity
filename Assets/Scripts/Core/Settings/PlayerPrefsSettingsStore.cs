#nullable enable
using UnityEngine;

namespace TinCan.Core.Settings
{
    /// <summary>Keeps the settings in PlayerPrefs, like the key bindings: per machine and user; an MPPM clone has its own.</summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public const string Key = "TinCan.PlayerSettings";

        public string? Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            return string.IsNullOrEmpty(json) ? null : json;
        }

        public void Save(string json)
        {
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
        }
    }
}
