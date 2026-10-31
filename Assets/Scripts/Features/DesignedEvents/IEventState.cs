#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// The designed event as every peer sees it: replicated by the EventState fixture on the ship, so hosts, clients and
    /// late joiners agree. The server writes it (<see cref="EventStateWriter"/>; the Server* calls do nothing elsewhere).
    /// Ids and indexes only: every peer builds the same catalog, so names and texts of phases come from there.
    /// </summary>
    public interface IEventState : IActor
    {
        /// <summary>The running event's id, 0 when none runs.</summary>
        int EventId { get; }

        int PhaseIndex { get; }

        /// <summary>The last line an event announced; empty when there is none to show.</summary>
        string Announcement { get; }

        /// <summary>The id of the event that finished last, 0 before the first.</summary>
        int LastEventId { get; }

        EventOutcome LastOutcome { get; }

        void ServerSetRunning(int eventId, int phaseIndex);
        void ServerSetFinished(int eventId, EventOutcome outcome);
        void ServerSetAnnouncement(string text);
    }
}
