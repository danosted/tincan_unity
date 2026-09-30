using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Domain.Events;

namespace TinCan.Tests.EditMode.Fakes
{
    public class FakeTimeService : ITimeService
    {
        public float Time { get; set; }
        public float DeltaTime { get; set; } = 1f / 30f;
        public float FixedDeltaTime { get; set; } = 0.02f;
        public int TickRate { get; set; } = 30;

        // Follows Time (floored to whole ticks) until a test sets it, so tests written in seconds keep working.
        private int? _tick;
        public int Tick
        {
            get => _tick ?? Mathf.FloorToInt(Time * TickRate + 1e-4f);
            set => _tick = value;
        }
    }

    public class FakeNetworkService : INetworkService
    {
        public NetworkState State => NetworkState.Offline;
        public bool IsActive => false;
        public bool IsServer => true;
        public bool IsClient => false;
        public bool IsHost => false;
        public ulong LocalClientId { get; set; } = 0;
        public string LastAddress { get; private set; } = string.Empty;
        public ushort LastPort { get; private set; }
        public int StartHostCalls { get; private set; }
        public int StartClientCalls { get; private set; }

        public void SetPlayerPrefab(GameObject prefab) { }
        public void SetConnection(string address, ushort port)
        {
            LastAddress = address;
            LastPort = port;
        }
        public void StartHost() => StartHostCalls++;
        public void StartServer() { }
        public void StartClient() => StartClientCalls++;
        public void Shutdown() { }
    }

    public class FakeActorRegistry : IActorRegistry
    {
        public event Action<IActor> OnActorRegistered;
        public event Action<IActor> OnActorUnregistered;

        private readonly List<IActor> _actors = new();

        public IEnumerable<IActor> AllActors => _actors;
        public IEnumerable<T> GetActors<T>() where T : IActor => _actors.OfType<T>();

        public bool TryGetActor(Guid id, out IActor actor)
        {
            actor = _actors.FirstOrDefault(a => a.Id == id);
            return actor != null;
        }

        /// <summary>The local player, for tests of owner-side logic; null by default.</summary>
        public IActor LocalPlayer { get; set; }

        public TActor GetLocalPlayerActor<TActor>() where TActor : IActor => LocalPlayer is TActor local ? local : default;

        public void Register(IActor actor)
        {
            _actors.Add(actor);
            OnActorRegistered?.Invoke(actor);
        }

        public void Unregister(IActor actor)
        {
            _actors.Remove(actor);
            OnActorUnregistered?.Invoke(actor);
        }
    }

    public class FakeAbilityRegistry : IAbilityRegistry
    {
        private readonly List<IAbilityControllerBase> _controllers = new();

        public IEnumerable<IAbilityControllerBase> AllControllers => _controllers;
        public void Register(IAbilityControllerBase controller) => _controllers.Add(controller);
        public void Unregister(IAbilityControllerBase controller) => _controllers.Remove(controller);
    }

    public class FakeEventPublisher : IEventPublisher
    {
        public void Publish<TEvent>(TEvent evt) { }
    }
}
