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
    /// cloud boundary uses, so the owner's prediction snaps instead of reconciling. It waits until the ship has a deck
    /// under that spot (<see cref="IShipDeckProbe"/>; a ship built from a design gets its hull a moment after it appears),
    /// at most <see cref="BoardingConfig.MaxDeckWait"/>. A character that appears before any ship stays where it spawned
    /// and is not revisited. Plan: .docs/plans/crew-gate-and-boarding.md.
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
        private readonly IShipDeckProbe _deck;
        private readonly ITimeService _time;
        private readonly HashSet<Guid> _seen = new();
        private readonly Dictionary<Guid, float> _waitingSince = new();

        public BoardingUseCase(INetworkService network, IActorRegistry actors, IHumanoidRespawnService respawn,
            IEventPublisher events, BoardingConfig config, IShipDeckProbe deck, ITimeService time)
        {
            _network = network;
            _actors = actors;
            _respawn = respawn;
            _events = events;
            _config = config;
            _deck = deck;
            _time = time;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            var players = _actors.GetActors<IHumanoidCharacterView>().Where(character => character.IsPlayerCharacter).ToList();
            if (_seen.Count > players.Count) _seen.IntersectWith(players.Select(character => character.Id));
            if (_waitingSince.Count > 0)
            {
                foreach (var gone in _waitingSince.Keys.Where(id => players.All(character => character.Id != id)).ToList())
                    _waitingSince.Remove(gone);
            }

            var ships = _actors.GetActors<IAirshipView>().Where(ship => ship.IsSimulating && ship.Transform != null).ToList();
            foreach (var character in players)
            {
                if (_seen.Contains(character.Id)) continue;
                if (ships.Count == 0)
                {
                    _seen.Add(character.Id);
                    continue;
                }

                var ship = Nearest(ships, character);
                var (position, rotation) = AirshipBoardingPose.Resolve(ship, _config.BoardingOffset);
                if (!_deck.HasDeckUnder(ship, position) && !WaitedTooLong(character.Id)) continue;

                _seen.Add(character.Id);
                _waitingSince.Remove(character.Id);
                _respawn.ResetCharacter(character, position, rotation);
                _events.Publish(new PlayerBoardedEvent(character.Id, ship.Id));
                _events.LogInfo(LogSource, $"Player {character.Id} boarded the ship.");
            }
        }

        private bool WaitedTooLong(Guid character)
        {
            if (!_waitingSince.TryGetValue(character, out float since))
            {
                _waitingSince[character] = since = _time.Time;
            }

            return _time.Time - since >= _config.MaxDeckWait;
        }

        private static IAirshipView Nearest(IReadOnlyList<IAirshipView> ships, IHumanoidCharacterView character)
        {
            var position = character.Movement.Transform.position;
            return ships.OrderBy(ship => (ship.Transform.position - position).sqrMagnitude).First();
        }
    }
}
