#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Items;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Application Layer, server.
    /// - **Overboard:** a player who falls overboard (<see cref="IOverboardHandler"/>, asked by the cloud boundary) is
    ///   marked with the overboard effect and left to choose. <see cref="OverboardHoldUseCase"/> holds them still; their
    ///   own peer sees the tag and offers the choice (<see cref="OverboardMenuUseCase"/>).
    /// - **Respawn:** once chosen (<see cref="IRespawnRequests"/>) and the rule's delay has passed, they respawn aboard the
    ///   nearest ship, and the rule decides what happens to what they carry (<see cref="RespawnRules"/>).
    /// - **Leash:** where the ship has a leash point, they may instead hang on it: marked leashed, moved by
    ///   <see cref="LeashRopeUseCase"/>. Reaching the point lands them on deck, keeping what they carry. Respawning stays
    ///   open while they hang.
    /// </summary>
    public sealed class OverboardUseCase : IOverboardHandler, ISimulationTickable
    {
        private sealed class Waiting
        {
            public IHumanoidCharacterView Player = null!;
            public float FellAt;
            public ActiveGameplayEffect? Overboard;
            public ActiveGameplayEffect? Leashed;
            public OverboardChoiceKind? Choice;
        }

        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private readonly RespawnRules _rules;
        private readonly LeashStore _leashes;
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly AbilitySystemUseCase _abilities;
        private readonly IHumanoidRespawnService _respawn;
        private readonly Dictionary<Guid, Waiting> _waiting = new();
        private readonly List<Guid> _done = new();

        public OverboardUseCase(RespawnRules rules, LeashStore leashes, INetworkService network, IActorRegistry actors, ITimeService time,
            IEventPublisher events, AbilitySystemUseCase abilities, IHumanoidRespawnService respawn)
        {
            _rules = rules;
            _leashes = leashes;
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _abilities = abilities;
            _respawn = respawn;
        }

        /// <summary>Server: whether <paramref name="playerId"/> is overboard or leashed, not yet aboard.</summary>
        public bool IsOverboard(Guid playerId) => _waiting.ContainsKey(playerId);

        /// <summary>Server: whether <paramref name="playerId"/> hangs on a leash.</summary>
        public bool IsLeashed(Guid playerId) => _waiting.TryGetValue(playerId, out var waiting) && waiting.Leashed != null;

        public bool TryHandleOverboard(IActor character)
        {
            if (!_network.IsServer || character is not IHumanoidCharacterView player || _rules.OverboardEffect == null) return false;
            if (_waiting.ContainsKey(player.Id)) return true;
            if (NearestShip(player) == null) return false;

            _waiting[player.Id] = new Waiting
            {
                Player = player,
                FellAt = _time.Time,
                Overboard = _abilities.ApplyEffect(player, _rules.OverboardEffect)
            };
            _events.Publish(new PlayerOverboardEvent(player.Id));
            return true;
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            foreach (var requests in _actors.GetActors<IRespawnRequests>().ToList())
            {
                foreach (var request in requests.TakeRequests()) Choose(request);
            }

            _done.Clear();
            foreach (var pair in _waiting)
            {
                if (Advance(pair.Value)) _done.Add(pair.Key);
            }

            foreach (var id in _done) _waiting.Remove(id);
        }

        // One player's next step; true once they are done (aboard, or gone).
        private bool Advance(Waiting waiting)
        {
            var player = waiting.Player;
            if (!player.IsSimulating)
            {
                _leashes.Detach(player.Id);
                return true;
            }

            var rule = RespawnProcessor.RuleFor(_rules.Rules, RespawnCause.Overboard);
            switch (waiting.Choice)
            {
                case OverboardChoiceKind.Respawn when RespawnProcessor.Remaining(waiting.FellAt, _time.Time, rule.DelaySeconds) <= 0f:
                    return TryRespawn(waiting, rule);
                case OverboardChoiceKind.Leash when waiting.Leashed == null:
                    TryLeash(waiting);
                    return false;
                case OverboardChoiceKind.Leash when _leashes.TryGet(player.Id, out var leash) && leash.Arrived:
                    Land(waiting, leash.Point);
                    return true;
                default:
                    return false;
            }
        }

        private void Choose(RespawnRequest request)
        {
            foreach (var waiting in _waiting.Values)
            {
                if (waiting.Player is not IPossessable possessable) continue;
                if (possessable.OwnerId != request.ClientId && possessable.PossessorId != request.ClientId) continue;

                // Respawning is always open; the leash only from overboard, and only once.
                if (request.Choice == OverboardChoiceKind.Respawn || waiting.Leashed == null) waiting.Choice = request.Choice;
            }
        }

        private void TryLeash(Waiting waiting)
        {
            var player = waiting.Player;
            var point = LeashPointOf(NearestShip(player));
            if (point == null || _rules.LeashedEffect == null)
            {
                _events.LogWarning("Respawn", "No leash point on the ship (or no leashed effect in the rules); only respawning is possible.");
                waiting.Choice = null;
                return;
            }

            ClearEffects(waiting);
            waiting.Leashed = _abilities.ApplyEffect(player, _rules.LeashedEffect);
            _leashes.Attach(player.Id, point, _rules.RopeLength);
            _events.Publish(new PlayerLeashedEvent(player.Id, point.Id));
        }

        private void Land(Waiting waiting, ILeashPoint point)
        {
            var player = waiting.Player;
            ClearEffects(waiting);
            _leashes.Detach(player.Id);
            if (!_leashes.AnyOn(point)) _leashes.StopWinch(point);

            var (position, rotation) = (point.Stand.position, point.Stand.rotation);
            _respawn.ResetCharacter(player, position, rotation);
            _events.Publish(new PlayerRescuedEvent(player.Id, point.Id));
        }

        private bool TryRespawn(Waiting waiting, RespawnRule rule)
        {
            var player = waiting.Player;
            var ship = NearestShip(player);
            if (ship == null) return false;

            string lost = "nothing";
            var equipment = EquipmentLocator.Resolve(player);
            if (rule.CarriedItem == CarriedItemOutcome.Lose && equipment is { IsEmptyHanded: false })
            {
                lost = equipment.Held?.name ?? "item";
                equipment.TryUnequip();
            }

            ClearEffects(waiting);
            if (_leashes.TryGet(player.Id, out var leash))
            {
                _leashes.Detach(player.Id);
                if (!_leashes.AnyOn(leash.Point)) _leashes.StopWinch(leash.Point);
            }

            var (position, rotation) = AirshipBoardingPose.Resolve(ship, _rules.BoardingOffset);
            _respawn.ResetCharacter(player, position, rotation);
            _events.Publish(new PlayerRespawnedEvent(player.Id, RespawnCause.Overboard, lost));
            return true;
        }

        private void ClearEffects(Waiting waiting)
        {
            if (waiting.Overboard != null) _abilities.RemoveEffect(waiting.Player, waiting.Overboard);
            if (waiting.Leashed != null) _abilities.RemoveEffect(waiting.Player, waiting.Leashed);
            waiting.Overboard = null;
            waiting.Leashed = null;
        }

        private ILeashPoint? LeashPointOf(IAirshipView? ship)
        {
            if (ship?.Transform == null) return null;
            return _actors.GetActors<ILeashPoint>().FirstOrDefault(point => point.IsSimulating && point.Ship == ship.Transform);
        }

        private IAirshipView? NearestShip(IHumanoidCharacterView player)
        {
            Vector3 position = player.Movement.Transform.position;
            return _actors.GetActors<IAirshipView>()
                .Where(ship => ship.IsSimulating && ship.Transform != null)
                .OrderBy(ship => (ship.Transform.position - position).sqrMagnitude)
                .FirstOrDefault();
        }
    }
}
