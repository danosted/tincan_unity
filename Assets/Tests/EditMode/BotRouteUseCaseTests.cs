#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.DevTools;
using TinCan.Features.Possession;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class BotRouteUseCaseTests
    {
        private sealed class LocalPlayerRegistry : IActorRegistry
        {
            public IActor? LocalPlayer;
            public readonly List<IActor> Actors = new();

            public event Action<IActor>? OnActorUnregistered;
            public IEnumerable<IActor> AllActors => Actors;
            public IEnumerable<T> GetActors<T>() where T : IActor => Actors.OfType<T>();
            public bool TryGetActor(Guid id, out IActor actor)
            {
                actor = Actors.FirstOrDefault(a => a.Id == id)!;
                return actor != null;
            }
            public TActor? GetLocalPlayerActor<TActor>() where TActor : IActor => LocalPlayer is TActor typed ? typed : default;
            public void Register(IActor actor) => Actors.Add(actor);
            public void Unregister(IActor actor)
            {
                Actors.Remove(actor);
                OnActorUnregistered?.Invoke(actor);
            }
        }

        private sealed class SessionNetwork : INetworkService
        {
            public bool IsActive { get; set; } = true;
            public bool IsServer { get; set; } = true;
            public NetworkState State => IsServer ? NetworkState.Host : NetworkState.Client;
            public bool IsClient => true;
            public bool IsHost => IsServer;
            public ulong LocalClientId => 0;
            public void SetPlayerPrefab(GameObject prefab) { }
            public void SetConnection(string address, ushort port) { }
            public void StartHost() { }
            public void StartServer() { }
            public void StartClient() { }
            public void Shutdown() { }
        }

        private sealed class RecordingPossessionAuthority : IPossessionAuthority
        {
            public int Acquired;
            public int Released;
            public bool TryAcquirePossession(Guid requesterActorId, IPossessable target)
            {
                Acquired++;
                return true;
            }
            public bool TryReleasePossession(Guid requesterActorId)
            {
                Released++;
                return true;
            }
        }

        private FakeHumanoidMovementView _movement = null!;
        private LocalPlayerRegistry _registry = null!;
        private SessionNetwork _network = null!;
        private RecordingPossessionAuthority _possession = null!;
        private ScriptedInput _input = null!;
        private HarnessSession _session = null!;
        private FakeTimeService _time = null!;

        [SetUp]
        public void SetUp()
        {
            _movement = new FakeHumanoidMovementView("BotPlayer");
            _registry = new LocalPlayerRegistry();
            _network = new SessionNetwork();
            _possession = new RecordingPossessionAuthority();
            _input = new ScriptedInput();
            _session = new HarnessSession();
            _time = new FakeTimeService { DeltaTime = 0.1f };
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
            foreach (var ship in _registry.GetActors<FakeAirshipView>()) UnityEngine.Object.DestroyImmediate(ship.GameObject);
        }

        private BotRouteUseCase Create(string route) => new(
            new HarnessOptions(null, route, false), _session, _input, _registry, _network, _possession, _time, new FakeEventPublisher());

        private void Tick(BotRouteUseCase useCase, float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / _time.DeltaTime);
            for (int i = 0; i < ticks; i++) useCase.Tick();
        }

        [Test]
        public void Tick_WaitsForLocalPlayer()
        {
            var useCase = Create("DeckWalk");

            Tick(useCase, 5f);

            Assert.That(_session.IsRouteRunning, Is.False);
        }

        [Test]
        public void Tick_PressesAndReleasesHeldActions()
        {
            _registry.LocalPlayer = new FakeHumanoidCharacterView(_movement);
            var useCase = Create("DeckWalk");

            Tick(useCase, 4.5f); // past the 4 s settle, inside "hold MoveForward"
            Assert.That(_input.IsPressed(ActionNames.MoveForward), Is.True);

            Tick(useCase, 1f); // into the following wait
            Assert.That(_input.IsPressed(ActionNames.MoveForward), Is.False);
        }

        [Test]
        public void Tick_RouteEnd_CompletesSessionAndReleasesInput()
        {
            _registry.LocalPlayer = new FakeHumanoidCharacterView(_movement);
            var useCase = Create("DeckWalk");
            bool completed = false;
            _session.RouteCompleted += () => completed = true;

            Tick(useCase, BotRoutes.DeckWalk.TotalDuration + 1f);

            Assert.That(completed, Is.True);
            Assert.That(_session.IsRouteComplete, Is.True);
            Assert.That(_input.IsPressed(ActionNames.MoveForward), Is.False);
            Assert.That(_input.IsPressed(ActionNames.Sprint), Is.False);
        }

        [Test]
        public void Pilot_OnHost_TakesAndReleasesHelm()
        {
            var player = new FakeHumanoidCharacterView(_movement);
            _registry.LocalPlayer = player;
            _registry.Register(new FakeAirshipView());
            var useCase = Create("Pilot");

            Tick(useCase, BotRoutes.Pilot.TotalDuration + 1f);

            Assert.That(_possession.Acquired, Is.EqualTo(1));
            Assert.That(_possession.Released, Is.EqualTo(1));
        }

        [Test]
        public void Pilot_OnClient_SkipsShipCommands()
        {
            _network.IsServer = false;
            _registry.LocalPlayer = new FakeHumanoidCharacterView(_movement);
            _registry.Register(new FakeAirshipView());
            var useCase = Create("Pilot");

            Tick(useCase, BotRoutes.Pilot.TotalDuration + 1f);

            Assert.That(_possession.Acquired, Is.Zero);
            Assert.That(_possession.Released, Is.Zero);
        }
    }
}
