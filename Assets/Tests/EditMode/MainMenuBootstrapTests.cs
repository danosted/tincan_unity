#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.UI;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Covers the single-owner Cancel handling: open/back/close, the vehicle-exit guard, session transitions,
    /// and the gameplay input gate.
    /// </summary>
    public class MainMenuBootstrapTests
    {
        private sealed class SwitchableNetwork : INetworkService
        {
            public NetworkState State { get; set; } = NetworkState.Offline;
            public bool IsActive => State != NetworkState.Offline;
            public bool IsServer => State == NetworkState.Host || State == NetworkState.Server;
            public bool IsClient => State == NetworkState.Client || State == NetworkState.Host;
            public bool IsHost => State == NetworkState.Host;
            public ulong LocalClientId => 0;
            public void SetPlayerPrefab(GameObject prefab) { }
            public void SetConnection(string address, ushort port) { }
            public void StartHost() => State = NetworkState.Host;
            public void StartServer() => State = NetworkState.Server;
            public void StartClient() => State = NetworkState.Client;
            public void Shutdown() => State = NetworkState.Offline;
        }

        private FakeInputService _input = null!;
        private SwitchableNetwork _network = null!;
        private FakePossessionState _possession = null!;
        private InputGate _gate = null!;
        private MenuUseCase _menus = null!;
        private MenuDefinition _main = null!;
        private MainMenuBootstrap _bootstrap = null!;
        private FakeAirshipView _body = null!;
        private FakeAirshipView _vehicle = null!;

        [SetUp]
        public void SetUp()
        {
            _input = new FakeInputService();
            _network = new SwitchableNetwork();
            _possession = new FakePossessionState();
            _gate = new InputGate();
            _menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[0]));
            _main = MenuDefinition.Create("main", "Main", new MenuItemDefinition { ItemId = "quit", Label = "Quit", Kind = MenuItemKind.Command, CommandId = "Quit" });
            _bootstrap = new MainMenuBootstrap(_menus, _network, _input, _possession, _gate, _main);
            _body = new FakeAirshipView("Body");
            _vehicle = new FakeAirshipView("Vehicle");
            _possession.PlayerActor = _body;
            _possession.CurrentPossession = _body;
        }

        [TearDown]
        public void TearDown()
        {
            _body.Destroy();
            _vehicle.Destroy();
            Object.DestroyImmediate(_main);
        }

        [Test]
        public void Initialize_Offline_OpensMenuAndBlocksGameplay()
        {
            _bootstrap.Initialize();

            Assert.That(_menus.IsOpen, Is.True);
            Assert.That(_gate.GameplayBlocked, Is.True);
        }

        [Test]
        public void SessionStarts_ClosesMenuAndUnblocksGameplay()
        {
            _bootstrap.Initialize();
            _network.StartHost();

            _bootstrap.Tick();

            Assert.That(_menus.IsOpen, Is.False);
            Assert.That(_gate.GameplayBlocked, Is.False);
        }

        [Test]
        public void Cancel_InOwnBody_OpensMenu_ThenCancelClosesIt_WithoutReopening()
        {
            _bootstrap.Initialize();
            _network.StartHost();
            _bootstrap.Tick();

            _input.TriggeredActions.Add(ActionNames.Cancel);
            _bootstrap.Tick();
            Assert.That(_menus.IsOpen, Is.True);
            Assert.That(_gate.GameplayBlocked, Is.True);

            _bootstrap.Tick();
            Assert.That(_menus.IsOpen, Is.False);
            Assert.That(_gate.GameplayBlocked, Is.False);
        }

        [Test]
        public void Cancel_WhileInVehicle_DoesNotOpenMenu()
        {
            _bootstrap.Initialize();
            _network.StartHost();
            _bootstrap.Tick();
            _possession.CurrentPossession = _vehicle;
            _bootstrap.Tick();

            _input.TriggeredActions.Add(ActionNames.Cancel);
            _bootstrap.Tick();

            Assert.That(_menus.IsOpen, Is.False);
        }

        [Test]
        public void Cancel_ThatExitedAVehicleThisFrame_DoesNotAlsoOpenMenu()
        {
            _bootstrap.Initialize();
            _network.StartHost();
            _bootstrap.Tick();
            _possession.CurrentPossession = _vehicle;
            _bootstrap.Tick();

            // The vehicle use case handled the same Cancel earlier this frame and restored the body.
            _possession.CurrentPossession = _body;
            _input.TriggeredActions.Add(ActionNames.Cancel);
            _bootstrap.Tick();
            Assert.That(_menus.IsOpen, Is.False);

            _bootstrap.Tick();
            Assert.That(_menus.IsOpen, Is.True, "a fresh press on the next frame opens it");
        }

        [Test]
        public void InputGate_AlwaysAllowsCancel()
        {
            _gate.GameplayBlocked = true;

            Assert.That(_gate.Allows(ActionNames.Cancel), Is.True);
            Assert.That(_gate.Allows(ActionNames.MoveForward), Is.False);
        }
    }
}
