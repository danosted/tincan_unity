#nullable enable
using System;
using TinCan.Core.Domain.Entities;
using Unity.Collections;
using Unity.Netcode;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Infrastructure Layer: the designed event's replicated state, on the EventState ship fixture (with an
    /// EntityNetworkMediator, which registers it as an actor). Server-written variables carry the running event and phase,
    /// the announcement and the last outcome to every peer, late joiners included.
    /// </summary>
    public class EventStateNetworkMediator : NetworkBehaviour, IEventState
    {
        private readonly NetworkVariable<int> _eventId = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _phaseIndex = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString128Bytes> _announcement = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _lastEventId = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<byte> _lastOutcome = new(
            (byte)EventOutcome.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private ActorIdentity? _identity;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public int EventId => _eventId.Value;
        public int PhaseIndex => _phaseIndex.Value;
        public string Announcement => _announcement.Value.ToString();
        public int LastEventId => _lastEventId.Value;
        public EventOutcome LastOutcome => (EventOutcome)_lastOutcome.Value;

        public void ServerSetRunning(int eventId, int phaseIndex)
        {
            if (!IsServer) return;
            _eventId.Value = eventId;
            _phaseIndex.Value = phaseIndex;
        }

        public void ServerSetFinished(int eventId, EventOutcome outcome)
        {
            if (!IsServer) return;
            _eventId.Value = 0;
            _phaseIndex.Value = 0;
            _lastEventId.Value = eventId;
            _lastOutcome.Value = (byte)outcome;
        }

        public void ServerSetAnnouncement(string text)
        {
            if (!IsServer) return;
            // Longer lines are cut to what the fixed string holds (127 UTF-8 bytes) rather than refused.
            var value = new FixedString128Bytes();
            value.CopyFromTruncated(text);
            _announcement.Value = value;
        }
    }
}
