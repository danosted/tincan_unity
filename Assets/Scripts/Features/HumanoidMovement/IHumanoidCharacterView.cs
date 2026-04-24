using TinCan.Features.Possession;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Features.FreeCamera;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: A composite interface representing a complete humanoid character.
    /// Combines movement, look, and ability capabilities.
    /// </summary>
    public interface IHumanoidCharacterView : ISimulatedActor<HumanoidInputState>, IPossessable, IAbilityController, IHasOrbitalCamera
    {
        IHumanoidMovementView Movement { get; }
    }
}
