#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>
    /// One row of an <see cref="IMenuRowSource"/>: a stable key (so focus stays on it as the list changes), its text,
    /// and whether it can be picked (a game of another version cannot).
    /// </summary>
    public readonly struct MenuSourceRow
    {
        public readonly string Key;
        public readonly string Label;
        public readonly bool Enabled;

        public MenuSourceRow(string key, string label, bool enabled = true)
        {
            Key = key;
            Label = label;
            Enabled = enabled;
        }
    }
}
