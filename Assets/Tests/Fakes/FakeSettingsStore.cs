#nullable enable
using TinCan.Core.Settings;

namespace TinCan.Tests.Fakes
{
    /// <summary>Settings saved in memory: what was loaded, and every save.</summary>
    public sealed class FakeSettingsStore : ISettingsStore
    {
        public string? Saved { get; set; }
        public int Saves { get; private set; }

        public string? Load() => Saved;

        public void Save(string json)
        {
            Saved = json;
            Saves++;
        }
    }
}
