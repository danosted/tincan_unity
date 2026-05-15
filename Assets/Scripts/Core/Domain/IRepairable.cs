using TinCan.Features.Interaction;
using TinCan.Core.Domain.Abilities;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Domain Layer: Interface for modules or ship parts that can be repaired.
    /// </summary>
    public interface IRepairable : IInteractable
    {
        IAbilityControllerBase Controller { get; }
        float HealthPercentage { get; }
        bool IsBroken { get; }
    }
}
