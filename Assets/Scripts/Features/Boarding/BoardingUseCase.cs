#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;

namespace TinCan.Features.Boarding
{
    /// <summary>
    /// Application Layer, server only, before the humanoids move: a player character seen for the first time while a
    /// ship exists is placed at that ship's boarding pose (<see cref="AirshipBoardingPose"/>), through the same reset the
    /// cloud boundary uses, so the owner's prediction snaps instead of reconciling. A character that appears before any
    /// ship stays where it spawned and is not revisited. Plan: .docs/plans/crew-gate-and-boarding.md.
    /// </summary>
    public class BoardingUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.BeforeHumanoid;

        private const string LogSource = "Boarding";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IHumanoidRespawnService _respawn;
        private readonly IEventPublisher _events;
        private readonly BoardingConfig _config;
        private readonly HashSet<Guid> _seen = new();

        public BoardingUseCase(INetworkService network, IActorRegistry actors, IHumanoidRespawnService respawn,
            IEventPublisher events, BoardingConfig config)
        {
            _network = network;
            _actors = actors;
            _respawn = respawn;
            _events = events;
            _config = config;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            var players = _actors.GetActors<IHumanoidCharacterView>().Where(character => character.IsPlayerCharacter).ToList();
            if (_seen.Count > players.Count) _seen.IntersectWith(players.Select(character => character.Id));

            var ships = _actors.GetActors<IAirshipView>().Where(ship => ship.IsSimulating && ship.Transform != null).ToList();
            foreach (var character in players)
            {
                if (!_seen.Add(character.Id) || ships.Count == 0) continue;
                Board(character, Nearest(ships, character));
            }
        }

        private void Board(IHumanoidCharacterView character, IAirshipView ship)
        {
            var (position, rotation) = AirshipBoardingPose.Resolve(ship, _config.BoardingOffset);
            _respawn.ResetCharacter(character, position, rotation);
            _events.Publish(new PlayerBoardedEvent(character.Id, ship.Id));
            _events.LogInfo(LogSource, $"Player {character.Id} boarded the ship.");
        }

        private static IAirshipView Nearest(IReadOnlyList<IAirshipView> ships, IHumanoidCharacterView character)
        {
            var position = character.Movement.Transform.position;
            return ships.OrderBy(ship => (ship.Transform.position - position).sqrMagnitude).First();
        }
    }
}
