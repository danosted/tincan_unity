#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Domain.Input;
using TinCan.Core.UI;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The main menu's lifecycle (opens offline, closes when a session starts) and the two Cancel handlers the input
    /// contexts route to: Back while a menu is open, Open when nothing above wanted Cancel.
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
            public void SetListenEndpoint(string listenAddress, ushort port) { }
            public void StartHost() => State = NetworkState.Host;
            public void StartServer() => State = NetworkState.Server;
            public void StartClient() => State = NetworkState.Client;
            public void Shutdown() => State = NetworkState.Offline;
        }

        private SwitchableNetwork _network = null!;
        private FakePossessionState _possession = null!;
        private MenuUseCase _menus = null!;
        private MenuDefinition _main = null!;
        private MainMenuBootstrap _bootstrap = null!;
        private IInputCommandHandler _open = null!;
        private IInputCommandHandler _back = null!;
        private OpenMenuCommand _openCommand = null!;
        private MenuBackCommand _backCommand = null!;
        private FakePossessable _body = null!;
        private FakePossessable _vehicle = null!;

        [SetUp]
        public void SetUp()
        {
            _network = new SwitchableNetwork();
            _possession = new FakePossessionState();
            _menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[0]));
            _main = MenuDefinition.Create("main", "Main", new MenuItemDefinition { ItemId = "quit", Label = "Quit", Kind = MenuItemKind.Command, CommandId = "Quit" });
            _bootstrap = new MainMenuBootstrap(_menus, _network, _main);
            _open = new OpenMenuInputHandler(_menus, _network, _possession, _main);
            _back = new MenuBackInputHandler(_menus);
            _openCommand = ScriptableObject.CreateInstance<OpenMenuCommand>();
            _backCommand = ScriptableObject.CreateInstance<MenuBackCommand>();
            _body = new FakePossessable();
            _vehicle = new FakePossessable();
            _possession.PlayerActor = _body;
            _possession.CurrentPossession = _body;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_main);
            Object.DestroyImmediate(_openCommand);
            Object.DestroyImmediate(_backCommand);
        }

        [Test]
        public void Initialize_Offline_OpensMenu()
        {
            _bootstrap.Initialize();

            Assert.That(_menus.IsOpen, Is.True);
        }

        [Test]
        public void SessionStarts_ClosesMenu()
        {
            _bootstrap.Initialize();
            _network.StartHost();

            _bootstrap.Tick();

            Assert.That(_menus.IsOpen, Is.False);
        }

        [Test]
        public void SessionEnds_ReopensMenu()
        {
            _bootstrap.Initialize();
            _network.StartHost();
            _bootstrap.Tick();

            _network.Shutdown();
            _bootstrap.Tick();

            Assert.That(_menus.IsOpen, Is.True);
        }

        [Test]
        public void OpenMenu_InOwnBody_Opens_ThenBackClosesIt()
        {
            _network.StartHost();

            Assert.That(_open.TryHandle(_openCommand), Is.True);
            Assert.That(_menus.IsOpen, Is.True);

            Assert.That(_back.TryHandle(_backCommand), Is.True);
            Assert.That(_menus.IsOpen, Is.False);
        }

        [Test]
        public void OpenMenu_WhilePossessingSomethingElse_Declines()
        {
            _network.StartHost();
            _possession.CurrentPossession = _vehicle;

            Assert.That(_open.TryHandle(_openCommand), Is.False);
            Assert.That(_menus.IsOpen, Is.False);
        }

        [Test]
        public void OpenMenu_Offline_OpensWhateverIsPossessed()
        {
            _possession.CurrentPossession = null;

            Assert.That(_open.TryHandle(_openCommand), Is.True);
            Assert.That(_menus.IsOpen, Is.True);
        }

        [Test]
        public void MenuBack_WithNoMenuOpen_Declines()
        {
            Assert.That(_back.TryHandle(_backCommand), Is.False);
        }

        [Test]
        public void Handlers_IgnoreOtherCommands()
        {
            Assert.That(_open.TryHandle(_backCommand), Is.False);
            Assert.That(_open.CommandType, Is.EqualTo(typeof(OpenMenuCommand)));
            Assert.That(_back.CommandType, Is.EqualTo(typeof(MenuBackCommand)));
        }
    }
}
