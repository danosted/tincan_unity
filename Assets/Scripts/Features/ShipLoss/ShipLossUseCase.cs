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

namespace TinCan.Features.ShipLoss
{
    /// <summary>
    /// Application Layer, server, after airship movement: the ship is lost with its whole crew when its health runs out or
    /// an <see cref="IShipLossSource"/> loses it (the cloud boundary: sunk too deep for too long). While lost it is held
    /// still where it went down (<see cref="IAirshipHold"/>) and every peer shows the Lost screen. Respawn brings it back
    /// at the last pose a loss source saw it safe (else where this use case first saw it): placed there, made whole
    /// (<see cref="IShipRecovery"/>), the crew put back aboard. Needs no voyage. Plan: .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    public sealed class ShipLossUseCase : ISimulationTickable, IShipLoss, IAirshipHold
    {
        private const string LogSource = "ShipLoss";

        /// <summary>Where the crew stands on a ship that has no boarding point, relative to the ship.</summary>
        private static readonly Vector3 BoardingOffset = new(0f, 3f, 0f);

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IEventPublisher _events;
        private readonly IReadOnlyList<IShipLossSource> _losses;
        private readonly IReadOnlyList<IShipRecovery> _recoveries;
        private readonly IHumanoidRespawnService _respawn;

        private bool _hasStart;
        private Vector3 _startPosition;
        private Quaternion _startRotation = Quaternion.identity;

        public ShipLossUseCase(INetworkService network, IActorRegistry actors, IEventPublisher events,
            IEnumerable<IShipLossSource> losses, IEnumerable<IShipRecovery> recoveries, IHumanoidRespawnService respawn)
        {
            _network = network;
            _actors = actors;
            _events = events;
            _losses = losses.ToArray();
            _recoveries = recoveries.ToArray();
            _respawn = respawn;
        }

        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        public bool IsLost => State()?.IsLost == true;

        public bool Holds(IAirshipView ship) => _network.IsServer && IsLost;

        public void Tick()
        {
            if (!_network.IsServer) return;

            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.IsSimulating && candidate.Transform != null);
            var state = State();
            if (ship == null || state == null) return;

            if (!_hasStart)
            {
                _startPosition = ship.Transform.position;
                _startRotation = AirshipUpright.Of(ship.Transform.rotation);
                _hasStart = true;
            }

            bool respawn = state.ConsumeRespawnRequest();
            if (state.IsLost)
            {
                if (respawn) Respawn(ship);
                return;
            }

            if (!IsLostNow(ship)) return;

            state.ServerSetLost(true);
            _events.LogInfo(LogSource, $"The ship is lost at {ship.Transform.position}, with its whole crew.");
        }

        public void BringBack(IAirshipView ship, Vector3 position, Quaternion rotation)
        {
            if (!_network.IsServer) return;

            if (ship is IAirshipTeleport teleport) teleport.Teleport(position, rotation);
            else ship.Transform.SetPositionAndRotation(position, rotation);

            foreach (var recovery in _recoveries) recovery.Recover(ship);

            var (deck, facing) = AirshipBoardingPose.Resolve(ship, BoardingOffset);
            foreach (var player in _actors.GetActors<IHumanoidCharacterView>().Where(character => character.IsPlayerCharacter).ToList())
                _respawn.ResetCharacter(player, deck, facing);

            State()?.ServerSetLost(false);
            _events.LogInfo(LogSource, $"The ship is back at {position}, whole, its crew aboard.");
        }

        private void Respawn(IAirshipView ship)
        {
            foreach (var loss in _losses)
            {
                if (loss.TryGetSafePose(ship, out var position, out var rotation))
                {
                    BringBack(ship, position, rotation);
                    return;
                }
            }

            BringBack(ship, _startPosition, _startRotation);
        }

        private bool IsLostNow(IAirshipView ship) =>
            (ship as IShipState)?.Controller.IsDepleted() == true || _losses.Any(loss => loss.IsLost(ship));

        private IShipLossState? State() => _actors.GetActors<IShipLossState>().FirstOrDefault();
    }
}
