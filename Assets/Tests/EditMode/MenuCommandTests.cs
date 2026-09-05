#nullable enable
using NUnit.Framework;
using TinCan.Features.UI;
using TinCan.Features.UI.Commands;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
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
            var join = new JoinGameMenuCommand(_network);
            _menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[] { join, new StartHostMenuCommand(_network) }));
            _joinMenu = MenuDefinition.Create("join", "Join",
                new MenuItemDefinition { ItemId = JoinGameMenuCommand.AddressItemId, Kind = MenuItemKind.TextField, DefaultValue = "" },
                new MenuItemDefinition { ItemId = JoinGameMenuCommand.PortItemId, Kind = MenuItemKind.TextField, DefaultValue = "" },
                new MenuItemDefinition { ItemId = "connect", Kind = MenuItemKind.Command, CommandId = JoinGameMenuCommand.Id },
                new MenuItemDefinition { ItemId = "host", Kind = MenuItemKind.Command, CommandId = StartHostMenuCommand.Id });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_joinMenu);

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
        public void StartHost_StartsHostOnce()
        {
            _menus.Open(_joinMenu);

            _menus.Invoke("host");

            Assert.That(_network.StartHostCalls, Is.EqualTo(1));
        }
    }
}
