using Unity.Netcode;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for a networked actor that can request interactions on the server.
    /// </summary>
    public interface IInteractionMediator
    {
        /// <summary>
        /// Requests the server to perform an interaction with a target object.
        /// </summary>
        /// <param name="target">The networked object to interact with.</param>
        void RequestInteract(NetworkObject target);
    }
}
