#nullable enable
namespace TinCan.Features.Airship.Fuel
{
    /// <summary>Server-owned stock of jerry cans on the ship (the crate at the bow).</summary>
    public interface IJerryCanSupply
    {
        int Count { get; }
        bool TryTake();
        void Add(int amount);
    }
}
