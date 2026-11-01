#nullable enable
namespace TinCan.Core.Settings
{
    /// <summary>Where the saved settings live: one JSON text of the values that differ from their default.</summary>
    public interface ISettingsStore
    {
        string? Load();
        void Save(string json);
    }
}
