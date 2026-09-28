#nullable enable
namespace TinCan.Features.DesignedEvents
{
    /// <summary>Server-side control and a read-only snapshot of the designed-event director, for dev tooling and HUD.</summary>
    public interface IEventDirector
    {
        /// <summary>Starts catalog events by itself (rotation with a quiet gap); starts from <see cref="EventDirectorSettings.AutoStart"/>.</summary>
        bool AutoStart { get; set; }

        EventDefinition? Active { get; }
        EventPhase? ActivePhase { get; }
        EventDefinition? LastFinished { get; }
        EventOutcome LastOutcome { get; }

        /// <summary>Server only. Refuses while another event runs, for an unknown id, or when a step has no handler.</summary>
        bool TryStart(int eventId, out string reason);
    }
}
