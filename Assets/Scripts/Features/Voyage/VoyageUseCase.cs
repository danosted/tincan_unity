#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// Application Layer, server only, after airship movement: runs the voyage. Begin resets every
    /// <see cref="ISessionParticipant"/> (a full tank, a whole hull, a clear sky), holds the pressure off and sets a
    /// destination ahead of the ship, with a new layout seed and the ship's position as the origin (the
    /// <see cref="ISessionLayout"/> world features build from); after the briefing it casts off and switches the
    /// pressure on. Underway, the ship
    /// arriving wins and its health running out loses; either way the pressure stops and the end screen shows until a
    /// player asks for a restart. A voyage starts by itself only once <see cref="VoyageConfig.MinCrew"/> players are
    /// aboard, and stands down to Idle whenever nobody is (<see cref="CrewQueries"/>). The replicated state lives on the
    /// VoyageState fixture (<see cref="IVoyageState"/>). Plans: .docs/plans/voyage-session.md,
    /// .docs/plans/crew-gate-and-boarding.md.
    /// </summary>
    public class VoyageUseCase : ISimulationTickable, IVoyage
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "Voyage";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly VoyageRouteProcessor _route;
        private readonly VoyageConfig _config;
        private readonly IReadOnlyList<ISessionParticipant> _participants;
        private readonly System.Random _random;

        private float _briefingLeft;
        private float _underwayFor;
        private int _voyage;
        private bool _pressureHeld;

        [Inject]
        public VoyageUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            VoyageRouteProcessor route, VoyageConfig config, IEnumerable<ISessionParticipant> participants, IRandomSource random)
            : this(network, actors, time, events, route, config, participants, random.Create("Voyage")) { }

        public VoyageUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            VoyageRouteProcessor route, VoyageConfig config, IEnumerable<ISessionParticipant> participants, System.Random random)
        {
            _random = random;
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _route = route;
            _config = config;
            _participants = participants.ToArray();
        }

        VoyagePhase IVoyage.Phase => _actors.GetActors<IVoyageState>().FirstOrDefault()?.Phase ?? VoyagePhase.Idle;

        public void Tick()
        {
            if (!_network.IsServer) return;

            // Until the first voyage begins (its state fixture spawns a little after the ship) nothing presses the crew.
            if (!_pressureHeld)
            {
                SetPressure(false);
                _pressureHeld = true;
            }

            if (!TryResolve(out var ship, out var state)) return;

            int crew = _actors.CrewCount();
            if (crew == 0)
            {
                state.ConsumeRestartRequest();
                if (state.Phase != VoyagePhase.Idle) StandDown(state);
                return;
            }

            if (state.ConsumeRestartRequest())
            {
                Begin(ship, state);
                return;
            }

            switch (state.Phase)
            {
                case VoyagePhase.Idle when _config.AutoStart && crew >= _config.MinCrew:
                    Begin(ship, state);
                    break;
                case VoyagePhase.Briefing:
                    _briefingLeft -= _time.DeltaTime;
                    state.ServerSetBriefingSecondsLeft(Mathf.CeilToInt(Mathf.Max(0f, _briefingLeft)));
                    if (_briefingLeft <= 0f) CastOff(state);
                    break;
                case VoyagePhase.Underway:
                    _underwayFor += _time.DeltaTime;
                    if ((ship as IShipState)?.Controller.IsDepleted() == true) End(state, VoyagePhase.Lost);
                    else if (_route.HasArrived(ship.Transform.position, state.Destination, _config.ArrivalRadius)) End(state, VoyagePhase.Arrived);
                    break;
            }
        }

        public bool Begin()
        {
            if (!_network.IsServer || !TryResolve(out var ship, out var state)) return false;
            Begin(ship, state);
            return true;
        }

        public bool CastOff()
        {
            if (!_network.IsServer || !TryResolve(out _, out var state) || state.Phase != VoyagePhase.Briefing) return false;
            CastOff(state);
            return true;
        }

        public bool MoveDestination(Vector3 destination)
        {
            if (!_network.IsServer || !TryResolve(out _, out var state)) return false;
            state.ServerSetDestination(destination);
            return true;
        }

        private void Begin(IAirshipView ship, IVoyageState state)
        {
            _voyage++;
            foreach (var participant in _participants) participant.ResetForSession();
            SetPressure(false);

            var destination = _route.Destination(ship.Transform.position, ship.Transform.rotation, _config.RouteLength);
            state.ServerSetDestination(destination);
            state.ServerSetLayout(_random.Next(1, int.MaxValue), ship.Transform.position);
            state.ServerSetVoyage(_voyage);
            _briefingLeft = _config.BriefingSeconds;
            _underwayFor = 0f;
            state.ServerSetBriefingSecondsLeft(Mathf.CeilToInt(_briefingLeft));
            state.ServerSetPhase(VoyagePhase.Briefing);

            _events.Publish(new VoyageStartedEvent(_voyage, destination));
            _events.LogInfo(LogSource, $"Voyage {_voyage}: briefing, destination {_config.RouteLength:0} m ahead.");
        }

        private void CastOff(IVoyageState state)
        {
            state.ServerSetBriefingSecondsLeft(0);
            state.ServerSetPhase(VoyagePhase.Underway);
            SetPressure(true);
            _events.LogInfo(LogSource, $"Voyage {_voyage}: underway.");
        }

        private void End(IVoyageState state, VoyagePhase outcome)
        {
            SetPressure(false);
            state.ServerSetPhase(outcome);
            _events.Publish(new VoyageEndedEvent(_voyage, outcome, _underwayFor));
            _events.LogInfo(LogSource, $"Voyage {_voyage}: {outcome} after {_underwayFor:0} s.");
        }

        /// <summary>Nobody is aboard: the pressure stops and the voyage waits in Idle; the next crew starts a fresh one.</summary>
        private void StandDown(IVoyageState state)
        {
            SetPressure(false);
            state.ServerSetBriefingSecondsLeft(0);
            state.ServerSetPhase(VoyagePhase.Idle);
            _events.LogInfo(LogSource, $"Voyage {_voyage}: the crew left, standing down.");
        }

        private void SetPressure(bool active)
        {
            foreach (var participant in _participants) participant.SetSessionActive(active);
        }

        private bool TryResolve(out IAirshipView ship, out IVoyageState state)
        {
            ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.IsSimulating && candidate.Transform != null)!;
            state = _actors.GetActors<IVoyageState>().FirstOrDefault()!;
            return ship != null && state != null;
        }
    }
}
