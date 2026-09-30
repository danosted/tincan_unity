#nullable enable

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Marks a player's humanoid body, so systems below the humanoid (GAS's ability grants) can recognise one without
    /// referencing it. Extended by <c>IHumanoidCharacterView</c>.
    /// </summary>
    public interface IHumanoidActor : IActor
    {
        /// <summary>
        /// A connected player's body (spawned as that client's player object), on every peer. The host's own player
        /// counts; a humanoid nobody plays would not. Counted by <see cref="CrewQueries"/>.
        /// </summary>
        bool IsPlayerCharacter { get; }
    }

    /// <summary>
    /// Marks the airship, so systems below the ship can recognise it without referencing it. Extended by
    /// <c>IAirshipView</c>.
    /// </summary>
    public interface IShipActor : IActor { }
}
