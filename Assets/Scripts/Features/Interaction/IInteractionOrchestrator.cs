namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for a service that handles interaction requests on the server.
    /// </summary>
    public interface IInteractionOrchestrator
    {
        void HandleInteraction(InteractionRequest request);
        void HandleExit();
    }
}
