#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;

namespace TinCan.Features.Encounters
{
    /// <summary>
    /// Server: the world is quiet outside encounter zones. The event features (auto-started designed events, random
    /// breakage, skill-check encounters, gas fields) take part through <see cref="ISessionParticipant"/>, and this keeps
    /// their pressure off while no zone is running. No zone exists yet, so the pressure stays off; the zones
    /// (encounters.md E1) turn it on while one runs. A profile loads this or the voyage, never both: both pace the same
    /// participants. Plan: .docs/plans/encounters.md (E0).
    /// </summary>
    public sealed class EncounterPressureUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private readonly INetworkService _network;
        private readonly IReadOnlyList<ISessionParticipant> _participants;
        private bool? _applied;

        public EncounterPressureUseCase(INetworkService network, IEnumerable<ISessionParticipant> participants)
        {
            _network = network;
            _participants = participants.ToList();
        }

        /// <summary>Whether the event features' pressure is on (server; false until a zone runs).</summary>
        public bool PressureOn => _applied == true;

        public void Tick()
        {
            if (!_network.IsServer)
            {
                _applied = null; // a later host start applies it again
                return;
            }

            const bool zoneRunning = false; // no encounter zones yet
            if (_applied == zoneRunning) return;

            _applied = zoneRunning;
            foreach (var participant in _participants) participant.SetSessionActive(zoneRunning);
        }
    }
}
