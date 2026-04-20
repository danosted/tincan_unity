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
        // Explicitly set the possessor on the server.
        void AuthoritativeSetPossessor(ulong? playerId);

        // Optional value indicating explicit possession.
        ulong? PossessorId { get; }

        ulong? OwnerId => (this as MonoBehaviour)?.GetComponent<NetworkObject>()?.OwnerClientId;

        bool IsCapturedBy(ulong playerId) => PossessorId == playerId;

        /// <summary>
        /// Checks if a player is allowed to possess this actor.
        /// </summary>
        bool CanPossess(ulong playerId);
    }
}
