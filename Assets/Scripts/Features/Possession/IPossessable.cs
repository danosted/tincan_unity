using TinCan.Core.Domain;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// An actor that can be possessed or owned by a player.
    /// </summary>
    public interface IPossessable : IActor
    {
        ulong? OwnerId { get; }     // Who "owns" this body/entity
        ulong? PossessorId { get; } // Who is currently "controlling" this entity

        void OnPossessed(ulong playerId);
        void OnUnpossessed();

        bool IsCapturedBy(ulong playerId) => PossessorId == playerId;
        bool IsIdleOwned(ulong playerId) => OwnerId == playerId && !PossessorId.HasValue;
    }
}
