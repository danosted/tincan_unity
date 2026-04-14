using UnityEngine;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Domain Layer: Interface for orchestrating the registration and unregistration
    /// of actors and their associated capabilities (interactors, etc.) within the system.
    /// </summary>
    public interface IActorOrchestrator
    {
        void RegisterHierarchy(GameObject root);
        void UnregisterHierarchy(GameObject root);
    }
}
