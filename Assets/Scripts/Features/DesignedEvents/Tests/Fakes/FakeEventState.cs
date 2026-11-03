#nullable enable
using System;
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.DesignedEvents.Tests
{
    /// <summary>IEventState as plain fields: the server calls write them, tests read them.</summary>
    public sealed class FakeEventState : IEventState
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating { get; set; } = true;
        public int EventId { get; private set; }
        public int PhaseIndex { get; private set; }
        public string Announcement { get; set; } = string.Empty;
        public int LastEventId { get; private set; }
        public EventOutcome LastOutcome { get; private set; }

        public void ServerSetRunning(int eventId, int phaseIndex) => (EventId, PhaseIndex) = (eventId, phaseIndex);

        public void ServerSetFinished(int eventId, EventOutcome outcome) =>
            (EventId, PhaseIndex, LastEventId, LastOutcome) = (0, 0, eventId, outcome);

        public void ServerSetAnnouncement(string text) => Announcement = text;
    }
}
