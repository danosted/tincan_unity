#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
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
    /// pressure on. Underway, the ship arriving wins; the ship lost (its health run out, or an
    /// <see cref="IShipLossSource"/> such as the cloud boundary) loses with its whole crew. Either way the pressure stops
    /// and the end screen shows. While the ship is lost it is held still (<see cref="IAirshipHold"/>) until a player
    /// chooses:
    /// - **Respawn:** the ship comes back whole (<see cref="IShipRecovery"/>) at its last safe spot (from a loss source,
    ///   else where the voyage began), the crew aboard, and the voyage carries on underway, its world as it was.
    /// - **Restart:** the ship comes back whole where the voyage began, and a new voyage begins from there.
    /// A voyage starts by itself only once <see cref="VoyageConfig.MinCrew"/> players are aboard, and stands down to Idle
    /// whenever nobody is (<see cref="CrewQueries"/>). The replicated state lives on the VoyageState fixture
    /// (<see cref="IVoyageState"/>). Plans: .docs/plans/voyage-session.md, .docs/plans/crew-gate-and-boarding.md,
    /// .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    public class VoyageUseCase : ISimulationTickable, IVoyage, IAirshipHold
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "Voyage";

        /// <summary>Where the crew stands on a ship that has no boarding point, relative to the ship.</summary>
        private static readonly Vector3 BoardingOffset = new(0f, 3f, 0f);

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly VoyageRouteProcessor _route;
        private readonly VoyageConfig _config;
        private readonly IReadOnlyList<ISessionParticipant> _participants;
        private readonly IReadOnlyList<IShipLossSource> _losses;
        private readonly IReadOnlyList<IShipRecovery> _recoveries;
        private readonly IHumanoidRespawnService _respawn;
        private readonly System.Random _random;

        private float _briefingLeft;
        private float _underwayFor;
        private int _voyage;
        private bool _pressureHeld;
        private Vector3 _originPosition;
        private Quaternion _originRotation = Quaternion.identity;

        [Inject]
        public VoyageUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            VoyageRouteProcessor route, VoyageConfig config, IEnumerable<ISessionParticipant> participants, IRandomSource random,
            IEnumerable<IShipLossSource> losses, IEnumerable<IShipRecovery> recoveries, IHumanoidRespawnService respawn)
            : this(network, actors, time, events, route, config, participants, random.Create("Voyage"), losses, recoveries, respawn) { }

        public VoyageUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            VoyageRouteProcessor route, VoyageConfig config, IEnumerable<ISessionParticipant> participants, System.Random random,
            IEnumerable<IShipLossSource> losses, IEnumerable<IShipRecovery> recoveries, IHumanoidRespawnService respawn)
        {
            _random = random;
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _route = route;
            _config = config;
            _participants = participants.ToArray();
            _losses = losses.ToArray();
            _recoveries = recoveries.ToArray();
            _respawn = respawn;
        }

        VoyagePhase IVoyage.Phase => CurrentPhase;

        private VoyagePhase CurrentPhase => _actors.GetActors<IVoyageState>().FirstOrDefault()?.Phase ?? VoyagePhase.Idle;

        /// <summary>A lost ship stays where it went down, behind the end screen, until the crew chooses.</summary>
        public bool Holds(IAirshipView ship) => _network.IsServer && CurrentPhase == VoyagePhase.Lost;

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
                state.ConsumeRespawnRequest();
                if (state.Phase != VoyagePhase.Idle) StandDown(state);
                return;
            }

            bool respawn = state.ConsumeRespawnRequest();
            if (state.ConsumeRestartRequest())
            {
                if (state.Phase == VoyagePhase.Lost) BringBack(ship, _originPosition, _originRotation);
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
                    if (IsLost(ship)) End(state, VoyagePhase.Lost);
                    else if (_route.HasArrived(ship.Transform.position, state.Destination, _config.ArrivalRadius)) End(state, VoyagePhase.Arrived);
                    break;
                case VoyagePhase.Lost when respawn:
                    Respawn(ship, state);
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

            _originPosition = ship.Transform.position;
            _originRotation = AirshipUpright.Of(ship.Transform.rotation);
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

        /// <summary>The crew chose to respawn: the ship whole at its last safe spot, and the same voyage goes on.</summary>
        private void Respawn(IAirshipView ship, IVoyageState state)
        {
            var (position, rotation) = SafePose(ship);
            BringBack(ship, position, rotation);
            state.ServerSetPhase(VoyagePhase.Underway);
            SetPressure(true);
            _events.LogInfo(LogSource, $"Voyage {_voyage}: the ship respawned at {position}, underway again.");
        }

        /// <summary>The lost ship returns: placed there, made whole, its crew put back aboard.</summary>
        private void BringBack(IAirshipView ship, Vector3 position, Quaternion rotation)
        {
            if (ship is IAirshipTeleport teleport) teleport.Teleport(position, rotation);
            else ship.Transform.SetPositionAndRotation(position, rotation);

            foreach (var recovery in _recoveries) recovery.Recover(ship);

            var (deck, facing) = AirshipBoardingPose.Resolve(ship, BoardingOffset);
            foreach (var player in _actors.GetActors<IHumanoidCharacterView>().Where(character => character.IsPlayerCharacter).ToList())
                _respawn.ResetCharacter(player, deck, facing);
        }

        private (Vector3 Position, Quaternion Rotation) SafePose(IAirshipView ship)
        {
            foreach (var loss in _losses)
            {
                if (loss.TryGetSafePose(ship, out var position, out var rotation)) return (position, rotation);
            }
            return (_originPosition, _originRotation);
        }

        private bool IsLost(IAirshipView ship) =>
            (ship as IShipState)?.Controller.IsDepleted() == true || _losses.Any(loss => loss.IsLost(ship));

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
