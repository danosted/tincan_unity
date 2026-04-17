using Unity.Netcode;
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for a service that handles interaction requests on the server.
    /// </summary>
    public interface IInteractionOrchestrator
    {
        void HandleInteraction(IInteractable target);
        void HandleExit();
    }
}
