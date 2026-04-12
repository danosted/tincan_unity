using TinCan.Features.Possession;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: A composite interface representing a complete humanoid character.
    /// Combines movement and look capabilities under a single possession authority.
    /// </summary>
    public interface IHumanoidCharacterView : IPossessable
    {
        IHumanoidMovementView Movement { get; }
        IHumanoidLookView Look { get; }
        HumanoidInputState InputState { get; set; }
    }
}
