#nullable enable
using UnityEngine;

namespace TinCan.Core.Input
{
    /// <summary>Keeps binding overrides in PlayerPrefs (per machine and user; an MPPM clone has its own).</summary>
    public sealed class PlayerPrefsInputBindingStore : IInputBindingStore
    {
        public const string Key = "TinCan.InputBindings";

        public string? Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            return string.IsNullOrEmpty(json) ? null : json;
        }

        public void Save(string overridesJson)
        {
            PlayerPrefs.SetString(Key, overridesJson);
            PlayerPrefs.Save();
        }
    }
}
