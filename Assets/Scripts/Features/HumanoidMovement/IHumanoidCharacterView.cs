using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Look;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: A composite interface representing a complete humanoid character.
    /// Combines movement, look, and ability capabilities.
    /// </summary>
    public interface IHumanoidCharacterView : ISimulatedActor<HumanoidInputState>, IPossessable, IAbilityController<HumanoidAttributeSet>, IHasOrbitalCamera, IHumanoidActor
    {
        IHumanoidMovementView Movement { get; }
    }
}
