#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
using VContainer;
using TinCan.Core.Domain.Networking;
using TinCan.DevTools;
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

            public event Action<IActor>? OnActorRegistered { add { } remove { } }
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
            public void SetListenEndpoint(string listenAddress, ushort port) { }
            public void StartHost() { }
            public void StartServer() { }
            public void StartClient() { }
            public void Shutdown() { }
        }

        private FakeHumanoidMovementView _movement = null!;
        private LocalPlayerRegistry _registry = null!;
        private SessionNetwork _network = null!;
        private FakeStationOccupancy _occupancy = null!;
        private ScriptedInput _input = null!;
        private TinCan.Core.Humanoid.HumanoidInputContext _humanoid = null!;
        private ScriptedActionDriver _driver = null!;
        private HarnessSession _session = null!;
        private FakeTimeService _time = null!;

        [SetUp]
        public void SetUp()
        {
            _movement = new FakeHumanoidMovementView("BotPlayer");
            _registry = new LocalPlayerRegistry();
            _network = new SessionNetwork();
            _occupancy = new FakeStationOccupancy();
            _input = new ScriptedInput();
            _humanoid = FakeInputContexts.Humanoid();
            var contexts = new VContainer.ContainerBuilder();
            contexts.RegisterInstance(_humanoid);
            contexts.RegisterInstance(FakeInputContexts.Helmsman());
            _driver = new ScriptedActionDriver(_input, new ScriptedActionMap(contexts.Build()), new FakeEventPublisher());
            _session = new HarnessSession();
            _time = new FakeTimeService { DeltaTime = 0.1f };
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
        }

        private BotRouteUseCase Create(string route) => new(
            new HarnessOptions(null, route, false), _session, _driver, _registry, _network, _occupancy, _time, new FakeEventPublisher());

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
            Assert.That(_input.IsPressed(_humanoid.Move!), Is.True);

            Tick(useCase, 1f); // into the following wait
            Assert.That(_input.IsPressed(_humanoid.Move!), Is.False);
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
            Assert.That(_input.IsPressed(_humanoid.Move!), Is.False);
            Assert.That(_input.IsPressed(_humanoid.Sprint!), Is.False);
        }

        [Test]
        public void Tick_BotLoop_RestartsTheRouteInsteadOfCompleting()
        {
            _registry.LocalPlayer = new FakeHumanoidCharacterView(_movement);
            var useCase = new BotRouteUseCase(new HarnessOptions(null, "DeckWalk", false, botLoop: true), _session, _driver,
                _registry, _network, _occupancy, _time, new FakeEventPublisher());
            bool completed = false;
            _session.RouteCompleted += () => completed = true;

            Tick(useCase, BotRoutes.DeckWalk.TotalDuration + 2f);

            Assert.That(completed, Is.False);
            Assert.That(_session.IsRouteRunning, Is.True);
        }

        [Test]
        public void Pilot_OnHost_TakesAndReleasesHelm()
        {
            var player = new FakeHumanoidCharacterView(_movement);
            _registry.LocalPlayer = player;
            _registry.Register(new FakeHelm());
            var useCase = Create("Pilot");

            Tick(useCase, BotRoutes.Pilot.TotalDuration + 1f);

            Assert.That(_occupancy.Taken, Is.EqualTo(1));
            Assert.That(_occupancy.Left, Is.EqualTo(1));
        }

        [Test]
        public void Pilot_OnClient_SkipsShipCommands()
        {
            _network.IsServer = false;
            _registry.LocalPlayer = new FakeHumanoidCharacterView(_movement);
            _registry.Register(new FakeHelm());
            var useCase = Create("Pilot");

            Tick(useCase, BotRoutes.Pilot.TotalDuration + 1f);

            Assert.That(_occupancy.Taken, Is.Zero);
            Assert.That(_occupancy.Left, Is.Zero);
        }
    }
}
