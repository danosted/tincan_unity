using TinCan.Features.Interaction;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for an actor or mediator that can request interactions over the network.
    /// </summary>
    public interface IInteractionRequester
    {
        void RequestInteraction(IInteractable target);
    }
}
