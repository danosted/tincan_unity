#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>
    /// A feature asking for a menu (<see cref="IMenuSystem.Request"/>). One request per owner; Back works inside it and
    /// never falls into another owner's menu. A pinned request cannot be dismissed with Back or Esc: it stays until its
    /// owner withdraws it.
    /// </summary>
    public readonly struct MenuRequest
    {
        public readonly string Owner;
        public readonly MenuDefinition Menu;
        public readonly MenuKind Kind;
        public readonly bool Pinned;

        public MenuRequest(string owner, MenuDefinition menu, MenuKind kind, bool pinned = false)
        {
            Owner = owner;
            Menu = menu;
            Kind = kind;
            Pinned = pinned;
        }
    }
}
