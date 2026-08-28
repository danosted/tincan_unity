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
    }

    public class FakeInputService : IInputService
    {
        public bool IsActionPressed(string actionName) => false;
        public bool WasActionTriggered(string actionName) => false;
        public float GetAxis(string positiveAction, string negativeAction) => 0f;
        public Vector2 GetMouseDelta() => Vector2.zero;
        public ulong GetActiveInputMask() => 0UL;
    }

    public class FakeNetworkService : INetworkService
    {
        public NetworkState State => NetworkState.Offline;
        public bool IsActive => false;
        public bool IsServer => true;
        public bool IsClient => false;
        public bool IsHost => false;
        public ulong LocalClientId { get; set; } = 0;

        public void SetPlayerPrefab(GameObject prefab) { }
        public void StartHost() { }
        public void StartServer() { }
        public void StartClient() { }
        public void Shutdown() { }
    }

    public class FakeActorRegistry : IActorRegistry
    {
        public event Action<IActor> OnActorUnregistered;

        private readonly List<IActor> _actors = new();

        public IEnumerable<IActor> AllActors => _actors;
        public IEnumerable<T> GetActors<T>() where T : IActor => _actors.OfType<T>();

        public bool TryGetActor(Guid id, out IActor actor)
        {
            actor = _actors.FirstOrDefault(a => a.Id == id);
            return actor != null;
        }

        public TActor GetLocalPlayerActor<TActor>() where TActor : IActor => default;

        public void Register(IActor actor) => _actors.Add(actor);
        public void Unregister(IActor actor)
        {
            _actors.Remove(actor);
            OnActorUnregistered?.Invoke(actor);
        }
    }

    public class FakeAbilityRegistry : IAbilityRegistry
    {
        public IEnumerable<IAbilityControllerBase> AllControllers => Enumerable.Empty<IAbilityControllerBase>();
        public void Register(IAbilityControllerBase controller) { }
        public void Unregister(IAbilityControllerBase controller) { }
    }

    public class FakeEventPublisher : IEventPublisher
    {
        public void Publish<TEvent>(TEvent evt) { }
    }
}
