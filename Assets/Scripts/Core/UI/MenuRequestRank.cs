#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>What <see cref="MenuArbitrationProcessor"/> needs of an open request: its kind and when it was made.</summary>
    public readonly struct MenuRequestRank
    {
        public readonly MenuKind Kind;
        public readonly int Sequence;

        public MenuRequestRank(MenuKind kind, int sequence)
        {
            Kind = kind;
            Sequence = sequence;
        }
    }
}
