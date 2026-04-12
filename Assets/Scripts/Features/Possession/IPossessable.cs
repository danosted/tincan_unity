using TinCan.Core.Domain;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// An actor that can be possessed or owned by a player.
    /// </summary>
    public interface IPossessable : IActor, IPossessionReceiver
    {
        ulong? OwnerId { get; }     // Who "owns" this body/entity
        ulong? PossessorId { get; } // Who is currently "controlling" this entity

        bool IsCapturedBy(ulong playerId) => PossessorId == playerId;
        bool IsIdleOwned(ulong playerId) => OwnerId == playerId && !PossessorId.HasValue;

        /// <summary>
        /// Checks if a player is allowed to possess this actor.
        /// </summary>
        bool CanPossess(ulong playerId) => !OwnerId.HasValue || OwnerId == playerId;
    }
}
