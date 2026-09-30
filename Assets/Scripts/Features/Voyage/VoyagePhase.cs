#nullable enable
namespace TinCan.Features.Voyage
{
    /// <summary>Where a voyage stands. Replicated as a byte, so keep the values stable.</summary>
    public enum VoyagePhase : byte
    {
        /// <summary>No voyage yet (the ship has just spawned).</summary>
        Idle = 0,

        /// <summary>Counting down to cast-off: the world is reset and holds still.</summary>
        Briefing = 1,

        /// <summary>Flying to the destination: hazards and breakage run.</summary>
        Underway = 2,

        /// <summary>Won: the ship reached the destination.</summary>
        Arrived = 3,

        /// <summary>Lost: the ship's health ran out.</summary>
        Lost = 4
    }
}
