using TinCan.Core.Domain;

namespace TinCan.Core.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for a service that handles interaction requests on the server.
    /// </summary>
    public interface IInteractionOrchestrator
    {
        void HandleInteraction(InteractionRequest request);

        /// <summary>Server: the requester interacts with a target the server acquired itself (no network id round-trip).</summary>
        void HandleInteraction(IActor requester, IInteractionTarget target);
    }
}
