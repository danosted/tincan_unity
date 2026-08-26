using TinCan.Features.Interaction;
using Unity.Netcode;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Resolves replicated interaction target identifiers to server-side interactables.
    /// </summary>
    public class NgoInteractionTargetResolver : IInteractionTargetResolver
    {
        private readonly NetworkManager _networkManager;

        public NgoInteractionTargetResolver(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        public bool TryResolve(InteractionTargetId targetId, out IInteractable target)
        {
            target = null!;
            if (!_networkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId.NetworkObjectId, out var targetObject))
            {
                return false;
            }

            target = targetObject.GetNetworkBehaviourAtOrderIndex(targetId.NetworkBehaviourId) as IInteractable;
            return target != null;
        }
    }
}
