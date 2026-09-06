#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Abilities;
using TinCan.Features.HumanoidMovement;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Application Layer: while a player carries the swinging-net tag (granted by the GA_SwingNet effect, driven by
    /// the predicted input bit), the server checks once per swing whether a flying can is within reach of the net
    /// head. A catch despawns the can and adds it to the ship's jerry-can supply. Ticked after humanoid movement.
    /// </summary>
    public class NetCatchUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private const string LogSource = "Fuel";

        private readonly INetworkService _networkService;
        private readonly IActorRegistry _actorRegistry;
        private readonly IFlyingCanSpawner _spawner;
        private readonly CatchProcessor _processor;
        private readonly FlyingCanConfig _config;
        private readonly IEventPublisher _eventPublisher;
        private readonly AbilitySystemUseCase _abilities;
        private readonly Dictionary<Guid, ActiveGameplayEffect> _caughtThisSwing = new();
        private readonly Dictionary<Guid, IFlyingCanView> _cans = new();
        private readonly List<(Guid Id, Vector3 Position)> _candidates = new();
        private readonly List<IHumanoidCharacterView> _players = new();

        public NetCatchUseCase(
            INetworkService networkService,
            IActorRegistry actorRegistry,
            IFlyingCanSpawner spawner,
            CatchProcessor processor,
            FlyingCanConfig config,
            IEventPublisher eventPublisher,
            AbilitySystemUseCase abilities)
        {
            _networkService = networkService;
            _actorRegistry = actorRegistry;
            _spawner = spawner;
            _processor = processor;
            _config = config;
            _eventPublisher = eventPublisher;
            _abilities = abilities;
        }

        public void Tick()
        {
            if (!_networkService.IsServer || _config == null || _config.SwingingTag == null) return;

            // Snapshot both sets: despawning a can unregisters it from the registry we are iterating.
            _cans.Clear();
            _candidates.Clear();
            foreach (var can in _actorRegistry.GetActors<IFlyingCanView>())
            {
                var canTransform = can.Transform;
                if (canTransform == null) continue;
                _cans.Add(can.Id, can);
                _candidates.Add((can.Id, canTransform.position));
            }
            _players.Clear();
            _players.AddRange(_actorRegistry.GetActors<IHumanoidCharacterView>());
            foreach (var id in _caughtThisSwing.Keys.Except(_players.Select(player => player.Id)).ToArray())
                _caughtThisSwing.Remove(id);

            foreach (var character in _players)
            {
                if (!character.HasTag(_config.SwingingTag) ||
                    !_abilities.TryGetActiveEffectGrantingTag(character.Id, _config.SwingingTag, out var swing))
                {
                    _caughtThisSwing.Remove(character.Id);
                    continue;
                }

                if (_caughtThisSwing.TryGetValue(character.Id, out var caughtSwing) && ReferenceEquals(caughtSwing, swing)) continue;
                if (_candidates.Count == 0) continue;
                TryCatch(character, swing);
            }
        }

        private void TryCatch(IHumanoidCharacterView character, ActiveGameplayEffect swing)
        {
            var body = character.Movement?.Transform;
            if (body == null) return;

            var netPosition = _processor.NetPosition(body.position, body.forward, _config.NetReach, _config.NetHeight);
            if (!_processor.TryFindCatchable(netPosition, _config.CatchRadius, _candidates, out var canId)) return;
            var can = _cans[canId];

            _caughtThisSwing[character.Id] = swing;
            _candidates.RemoveAll(candidate => candidate.Id == canId);
            _cans.Remove(canId);
            _spawner.Despawn(can);

            var supply = ResolveSupply();
            supply?.Add(1);
            if (supply == null) _eventPublisher.LogWarning(LogSource, "Caught a can but no jerry-can supply exists on any airship.");

            _eventPublisher.Publish(new JerryCanCaughtEvent(character.Id, supply?.Count ?? 0));
        }

        private IJerryCanSupply? ResolveSupply()
        {
            foreach (var airship in _actorRegistry.GetActors<IAirshipView>())
            {
                var supply = FuelTankLocator.FindFixture<IJerryCanSupply>(airship);
                if (supply != null) return supply;
            }
            return null;
        }
    }
}
