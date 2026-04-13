using TinCan.Features.Possession;
using TinCan.Core.Domain;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: A composite interface representing a complete humanoid character.
    /// Combines movement and look capabilities under a single possession authority.
    /// </summary>
    public interface IHumanoidCharacterView : ISimulatedPossessable<HumanoidInputState>, IPossessable
    {
        IHumanoidMovementView Movement { get; }
        IHumanoidLookView Look { get; }
    }
}
