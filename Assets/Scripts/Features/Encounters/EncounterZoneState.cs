#nullable enable
namespace TinCan.Features.Encounters
{
    /// <summary>Where an encounter zone is in its round (encounters.md, "The flow"). Replicated as a byte.</summary>
    public enum EncounterZoneState : byte
    {
        /// <summary>Waiting for a ship to sail in.</summary>
        Idle,

        /// <summary>A ship crossed in: the cue runs; turning back out before it ends means nothing starts.</summary>
        Warning,

        /// <summary>The zone's event runs; sailing out abandons it.</summary>
        Running,

        /// <summary>Spent and calm for a while, then idle again.</summary>
        Resting,

        /// <summary>Spent for good (EncounterAfterEnd.Gone).</summary>
        Gone
    }
}
