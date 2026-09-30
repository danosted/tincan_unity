#nullable enable
using System;

namespace TinCan.Features.Boarding
{
    /// <summary>Server: a player who spawned while a ship existed was placed on board.</summary>
    public readonly struct PlayerBoardedEvent
    {
        public readonly Guid CharacterId;
        public readonly Guid AirshipId;

        public PlayerBoardedEvent(Guid characterId, Guid airshipId)
        {
            CharacterId = characterId;
            AirshipId = airshipId;
        }
    }
}
