using TinCan.Core.Domain;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for any object in the world that can be interacted with by a player.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Called when a player successfully interacts with this object.
        /// </summary>
        /// <param name="interactorId">The network ID of the player who interacted.</param>
        void OnInteract(ulong interactorId);
    }
}
