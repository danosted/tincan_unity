#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>A design the store can load: its key and whether it ships with the game (read-only).</summary>
    public readonly struct ShipDesignListing
    {
        public ShipDesignListing(string key, bool isBuiltIn)
        {
            Key = key;
            IsBuiltIn = isBuiltIn;
        }

        public string Key { get; }
        public bool IsBuiltIn { get; }

        public override string ToString() => IsBuiltIn ? $"{Key} (built in)" : Key;
    }
}
