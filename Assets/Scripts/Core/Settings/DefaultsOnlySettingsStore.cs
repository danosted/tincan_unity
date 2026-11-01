#nullable enable
namespace TinCan.Core.Settings
{
    /// <summary>
    /// <c>-defaultsettings</c>: reads nothing and writes nothing, so scenario, bot and perf runs never change with what a
    /// developer saved. Changes made during the run still apply until it ends.
    /// </summary>
    public sealed class DefaultsOnlySettingsStore : ISettingsStore
    {
        public const string Flag = "-defaultsettings";

        public string? Load() => null;

        public void Save(string json) { }
    }
}
