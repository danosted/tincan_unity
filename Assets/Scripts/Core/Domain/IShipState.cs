using TinCan.Core.Domain.Abilities;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Domain contract for the ship's synchronized state.
    /// Exposes the ship's ability controller so gameplay systems can query the actor's active GAS state.
    /// </summary>
    public interface IShipState : IActor
    {
        IAbilityControllerBase Controller { get; }
    }
}
