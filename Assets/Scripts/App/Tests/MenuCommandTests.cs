#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Networking;
using TinCan.Core.UI;
using TinCan.Core.UI.Commands;
using TinCan.Network.Infrastructure.Session;
using UnityEngine;
using TinCan.Network.Infrastructure.Tests;
using TinCan.Tests.Fakes;

namespace TinCan.App.Tests
{
    public class MenuCommandTests
    {
        private FakeNetworkService _network = null!;
        private MenuUseCase _menus = null!;
        private MenuDefinition _joinMenu = null!;

        [SetUp]
        public void SetUp()
        {
            _network = new FakeNetworkService();
            _menus = Menus(FakeSessions.Direct(_network));
            _joinMenu = JoinMenu();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_joinMenu);

        private static MenuUseCase Menus(ISessionService session) =>
            new(new MenuCommandRegistry(new IMenuCommand[] { new JoinGameMenuCommand(session), new StartHostMenuCommand(session, new FakeEventPublisher()) }));

        private static MenuDefinition JoinMenu() => MenuDefinition.Create("join", "Join",
            new MenuItemDefinition { ItemId = JoinGameMenuCommand.AddressItemId, Kind = MenuItemKind.TextField, DefaultValue = "" },
            new MenuItemDefinition { ItemId = JoinGameMenuCommand.PortItemId, Kind = MenuItemKind.TextField, DefaultValue = "" },
            new MenuItemDefinition { ItemId = JoinGameMenuCommand.StatusItemId, Kind = MenuItemKind.Note },
            new MenuItemDefinition { ItemId = "connect", Kind = MenuItemKind.Command, CommandId = JoinGameMenuCommand.Id },
            new MenuItemDefinition { ItemId = "host", Kind = MenuItemKind.Command, CommandId = StartHostMenuCommand.Id });

        private string? Status(MenuUseCase menus) =>
            menus.Current!.Items.Where(r => r.ItemId == JoinGameMenuCommand.StatusItemId).Select(r => r.Label).FirstOrDefault();

        [Test]
        public void JoinGame_UsesEnteredAddressAndPort()
        {
            _menus.Open(_joinMenu);
            _menus.SetValue(JoinGameMenuCommand.AddressItemId, " 10.0.0.7 ");
            _menus.SetValue(JoinGameMenuCommand.PortItemId, "8000");

            _menus.Invoke("connect");

            Assert.That(_network.LastAddress, Is.EqualTo("10.0.0.7"));
            Assert.That(_network.LastPort, Is.EqualTo(8000));
            Assert.That(_network.StartClientCalls, Is.EqualTo(1));
        }

        [Test]
        public void JoinGame_FallsBackToDefaultsWhenFieldsAreEmptyOrInvalid()
        {
            _menus.Open(_joinMenu);
            _menus.SetValue(JoinGameMenuCommand.PortItemId, "not-a-port");

            _menus.Invoke("connect");

            Assert.That(_network.LastAddress, Is.EqualTo(JoinGameMenuCommand.DefaultAddress));
            Assert.That(_network.LastPort, Is.EqualTo(JoinGameMenuCommand.DefaultPort));
        }

        [Test]
        public void JoinGame_StatusLine_IsHiddenUntilThereIsSomethingToSay()
        {
            _menus.Open(_joinMenu);

            Assert.That(Status(_menus), Is.Null);
        }

        [Test]
        public void JoinGame_SaysConnecting_ThenWhyTheHostRefused()
        {
            var network = new FakeSessionNetworkService { IsActive = false, IsClient = true, LocalClientId = 3 };
            var connections = new FakeNetworkConnections();
            var menus = Menus(FakeSessions.Direct(network, connections));
            menus.Open(_joinMenu);
            menus.SetValue(JoinGameMenuCommand.AddressItemId, "10.0.0.7");

            menus.Invoke("connect");
            Assert.That(Status(menus), Is.EqualTo(JoinGameMenuCommand.ConnectingMessage(new JoinTarget("10.0.0.7", JoinGameMenuCommand.DefaultPort))));

            connections.Disconnect(3, ConnectionApprovalProcessor.FullReason(8));
            Assert.That(Status(menus), Is.EqualTo(ConnectionApprovalProcessor.FullReason(8)));
        }

        [Test]
        public void JoinGame_LetIn_ClearsTheStatusLine()
        {
            var network = new FakeSessionNetworkService { IsActive = false, IsClient = true, LocalClientId = 3 };
            var connections = new FakeNetworkConnections();
            var menus = Menus(FakeSessions.Direct(network, connections));
            menus.Open(_joinMenu);

            menus.Invoke("connect");
            connections.ConnectedAsClient(3);

            Assert.That(Status(menus), Is.Null);
        }

        [Test]
        public void StartHost_StartsHostOnce()
        {
            _menus.Open(_joinMenu);

            _menus.Invoke("host");

            Assert.That(_network.StartHostCalls, Is.EqualTo(1));
        }
    }
}
