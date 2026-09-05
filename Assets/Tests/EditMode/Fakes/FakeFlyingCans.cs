#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Features.Airship.Fuel.Minigame;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    public sealed class FakeFlyingCanView : IFlyingCanView
    {
        private readonly GameObject _gameObject;

        public FakeFlyingCanView(Vector3 position)
        {
            _gameObject = new GameObject("FakeFlyingCan");
            _gameObject.transform.position = position;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public Transform Transform => _gameObject.transform;
        public Vector3 Velocity { get; set; }
        public float SpawnTime { get; set; }

        public void Destroy()
        {
            if (_gameObject != null) UnityEngine.Object.DestroyImmediate(_gameObject);
        }
    }

    /// <summary>Spawner that registers cans into the given registry so the use case sees them on the next tick.</summary>
    public sealed class FakeFlyingCanSpawner : IFlyingCanSpawner
    {
        private readonly FakeActorRegistry _registry;

        public FakeFlyingCanSpawner(FakeActorRegistry registry)
        {
            _registry = registry;
        }

        public List<FakeFlyingCanView> Spawned { get; } = new();
        public List<IFlyingCanView> Despawned { get; } = new();
        public bool RefuseSpawns { get; set; }

        public IFlyingCanView? Spawn(Vector3 position, Vector3 velocity)
        {
            if (RefuseSpawns) return null;
            var can = new FakeFlyingCanView(position) { Velocity = velocity };
            Spawned.Add(can);
            _registry.Register(can);
            return can;
        }

        public void Despawn(IFlyingCanView can)
        {
            Despawned.Add(can);
            _registry.Unregister(can);
            (can as FakeFlyingCanView)?.Destroy();
        }

        public void DestroyAll()
        {
            foreach (var can in Spawned) can.Destroy();
            Spawned.Clear();
        }
    }
}
