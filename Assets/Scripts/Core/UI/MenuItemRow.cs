#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>Immutable view model of one menu row; views render these without knowing the definition asset.</summary>
    public readonly struct MenuItemRow
    {
        public readonly string ItemId;
        public readonly string Label;
        public readonly MenuItemKind Kind;
        public readonly string Value;

        public MenuItemRow(string itemId, string label, MenuItemKind kind, string value)
        {
            ItemId = itemId;
            Label = label;
            Kind = kind;
            Value = value;
        }
    }
}
