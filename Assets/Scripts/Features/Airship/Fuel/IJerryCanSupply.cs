#nullable enable
namespace TinCan.Features.Airship.Fuel
{
    /// <summary>Server-owned stock of jerry cans on the ship (the crate at the bow).</summary>
    public interface IJerryCanSupply
    {
        /// <summary>The item a taken can becomes in the player's hands.</summary>
        TinCan.Features.Items.ItemDefinition? Item { get; }
        int Count { get; }
        bool TryTake();
        void Add(int amount);
    }
}
