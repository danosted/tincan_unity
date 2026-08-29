#nullable enable
using System;

namespace TinCan.Features.CloudBoundary
{
    public readonly struct CloudEmergencyStartedEvent
    {
        public Guid AirshipId { get; }
        public float Duration { get; }

        public CloudEmergencyStartedEvent(Guid airshipId, float duration)
        {
            AirshipId = airshipId;
            Duration = duration;
        }
    }

    public readonly struct CloudEmergencyClearedEvent
    {
        public Guid AirshipId { get; }

        public CloudEmergencyClearedEvent(Guid airshipId)
        {
            AirshipId = airshipId;
        }
    }

    public readonly struct CloudEmergencyExpiredEvent
    {
        public Guid AirshipId { get; }

        public CloudEmergencyExpiredEvent(Guid airshipId)
        {
            AirshipId = airshipId;
        }
    }

    public readonly struct CloudCharacterResetEvent
    {
        public Guid CharacterId { get; }
        public Guid AirshipId { get; }

        public CloudCharacterResetEvent(Guid characterId, Guid airshipId)
        {
            CharacterId = characterId;
            AirshipId = airshipId;
        }
    }
}
