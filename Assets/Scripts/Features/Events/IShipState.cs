using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;

namespace TinCan.Features.Events
{
    /// <summary>
    /// Domain Interface for the ship's synchronized state.
    /// Exposes the ship's Ability Controller to provide GAS capabilities.
    /// </summary>
    public interface IShipState : IActor
    {
        IAbilityControllerBase Controller { get; }
    }
}
