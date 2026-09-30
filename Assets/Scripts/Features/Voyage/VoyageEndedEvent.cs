#nullable enable
namespace TinCan.Features.Voyage
{
    /// <summary>Server: a voyage ended, won (Arrived) or lost; <see cref="Seconds"/> counts from cast-off.</summary>
    public readonly struct VoyageEndedEvent
    {
        public readonly int Voyage;
        public readonly VoyagePhase Outcome;
        public readonly float Seconds;

        public VoyageEndedEvent(int voyage, VoyagePhase outcome, float seconds)
        {
            Voyage = voyage;
            Outcome = outcome;
            Seconds = seconds;
        }
    }
}
