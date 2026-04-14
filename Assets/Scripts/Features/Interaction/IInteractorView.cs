using TinCan.Core.Domain;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for an actor (like a player character) that can scan for and interact with IInteractable objects.
    /// </summary>
    public interface IInteractorView
    {
        /// <summary>
        /// The actor that owns this interactor capability.
        /// </summary>
        IActor Owner { get; }

        /// <summary>
        /// The currently focused interactable object, if any.
        /// </summary>
        IInteractable CurrentTarget { get; }
    }
}
