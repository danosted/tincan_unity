using TinCan.Core.Domain;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// An actor that can be possessed or owned by a player.
    /// Focuses purely on capability and validation.
    /// </summary>
    public interface IPossessable : IActor
    {
        // Default implementations to derive state from the underlying NetworkObject
        ulong? OwnerId => (this as MonoBehaviour)?.GetComponent<NetworkObject>()?.OwnerClientId;
        ulong? PossessorId => OwnerId;

        bool IsCapturedBy(ulong playerId) => PossessorId == playerId;

        /// <summary>
        /// Checks if a player is allowed to possess this actor.
        /// </summary>
        bool CanPossess(ulong playerId);
    }
}
