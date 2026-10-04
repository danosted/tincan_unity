#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Core.Ship.Fixtures
{
    /// <summary>
    /// Application Layer (server only): furnishes every airship once with the fixtures contributed by feature
    /// installers, spawning each as its own NetworkObject parented to the ship. A feature can keep a fixture off a ship
    /// through <see cref="IShipFixtureFilter"/> (ship designs place their own helm).
    /// </summary>
    public class ShipFixtureSpawningUseCase : IInitializable, ITickable, IDisposable
    {
        private const string LogSource = "ShipFixtures";

        private readonly INetworkService _networkService;
        private readonly IActorRegistry _actorRegistry;
        private readonly IModuleSpawningService _moduleSpawning;
        private readonly IShipFixtureCatalog _catalog;
        private readonly IEventPublisher _eventPublisher;
        private readonly IReadOnlyList<IShipFixtureFilter> _filters;
        private readonly HashSet<Guid> _furnished = new();
        private readonly List<IAirshipView> _scratch = new();

        public ShipFixtureSpawningUseCase(
            INetworkService networkService,
            IActorRegistry actorRegistry,
            IModuleSpawningService moduleSpawning,
            IShipFixtureCatalog catalog,
            IEventPublisher eventPublisher,
            IReadOnlyList<IShipFixtureFilter>? filters = null)
        {
            _networkService = networkService;
            _actorRegistry = actorRegistry;
            _moduleSpawning = moduleSpawning;
            _catalog = catalog;
            _eventPublisher = eventPublisher;
            _filters = filters ?? Array.Empty<IShipFixtureFilter>();
        }

        public void Initialize() => _actorRegistry.OnActorUnregistered += HandleActorUnregistered;

        public void Dispose() => _actorRegistry.OnActorUnregistered -= HandleActorUnregistered;

        public void Tick()
        {
            if (!_networkService.IsServer || _catalog.Fixtures.Count == 0) return;

            _scratch.Clear();
            _scratch.AddRange(_actorRegistry.GetActors<IAirshipView>());

            foreach (var airship in _scratch)
            {
                if (!airship.IsSimulating || _furnished.Contains(airship.Id)) continue;

                var shipTransform = airship.Transform;
                if (shipTransform == null) continue;

                _furnished.Add(airship.Id);
                Furnish(airship, shipTransform);
            }
        }

        private void Furnish(IAirshipView airship, Transform shipTransform)
        {
            int spawned = 0;
            foreach (var fixture in _catalog.Fixtures)
            {
                if (fixture.Prefab == null || !Allowed(airship, fixture)) continue;

                var position = shipTransform.TransformPoint(fixture.LocalPosition);
                var rotation = shipTransform.rotation * Quaternion.Euler(fixture.LocalEulerAngles);
                _moduleSpawning.SpawnModule(fixture.Prefab, position, rotation, airship);
                spawned++;
            }

            _eventPublisher.LogInfo(LogSource, $"Spawned {spawned} fixture(s) on airship {airship.Id}.");
        }

        private bool Allowed(IAirshipView airship, ShipFixtureDefinition fixture)
        {
            foreach (var filter in _filters)
            {
                if (!filter.ShouldSpawn(airship, fixture)) return false;
            }

            return true;
        }

        private void HandleActorUnregistered(IActor actor) => _furnished.Remove(actor.Id);
    }
}
