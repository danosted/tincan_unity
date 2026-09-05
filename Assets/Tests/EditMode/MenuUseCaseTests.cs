#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Features.UI;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Covers the headless menu API: the stack, value storage and command dispatch.
    /// Views are not involved; a view is only a renderer of <see cref="MenuSnapshot"/>.
    /// </summary>
    public class MenuUseCaseTests
    {
        private FakeMenuCommand _connectCommand = null!;
        private MenuUseCase _menus = null!;
        private MenuDefinition _joinMenu = null!;
        private MenuDefinition _mainMenu = null!;
        private readonly List<ScriptableObject> _createdAssets = new();
        private int _changedCount;

        [SetUp]
        public void SetUp()
        {
            _changedCount = 0;
            _connectCommand = new FakeMenuCommand("Connect");
            _menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[] { _connectCommand }));
            _menus.Changed += () => _changedCount++;

            _joinMenu = Track(MenuDefinition.Create("join", "Join",
                new MenuItemDefinition { ItemId = "address", Label = "Address", Kind = MenuItemKind.TextField, DefaultValue = "127.0.0.1" },
                new MenuItemDefinition { ItemId = "connect", Label = "Connect", Kind = MenuItemKind.Command, CommandId = "Connect" },
                new MenuItemDefinition { ItemId = "back", Label = "Back", Kind = MenuItemKind.Back }));

            _mainMenu = Track(MenuDefinition.Create("main", "Main",
                new MenuItemDefinition { ItemId = "host", Label = "Host", Kind = MenuItemKind.Command, CommandId = "Missing" },
                new MenuItemDefinition { ItemId = "join", Label = "Join", Kind = MenuItemKind.Submenu, Submenu = _joinMenu },
                new MenuItemDefinition { ItemId = "mute", Label = "Mute", Kind = MenuItemKind.Toggle, DefaultValue = "False" }));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _createdAssets) Object.DestroyImmediate(asset);
            _createdAssets.Clear();
        }

        [Test]
        public void Open_ExposesSnapshotWithRowsAndDefaultValues()
        {
            _menus.Open(_mainMenu);

            Assert.That(_menus.IsOpen, Is.True);
            Assert.That(_menus.Current!.Title, Is.EqualTo("Main"));
            Assert.That(_menus.Current.Items.Count, Is.EqualTo(3));
            Assert.That(_menus.Current.CanGoBack, Is.False);
            Assert.That(_menus.GetValue("mute"), Is.EqualTo("False"));
            Assert.That(_changedCount, Is.EqualTo(1));
        }

        [Test]
        public void Invoke_Submenu_PushesAndBackPops()
        {
            _menus.Open(_mainMenu);
            _menus.Invoke("join");

            Assert.That(_menus.Current!.MenuId, Is.EqualTo("join"));
            Assert.That(_menus.Current.CanGoBack, Is.True);

            _menus.Invoke("back");

            Assert.That(_menus.Current!.MenuId, Is.EqualTo("main"));
        }

        [Test]
        public void Invoke_Command_RoutesToRegisteredCommandWithMenuValues()
        {
            _menus.Open(_joinMenu);
            _menus.SetValue("address", "10.0.0.5");

            _menus.Invoke("connect");

            Assert.That(_connectCommand.Executions.Count, Is.EqualTo(1));
            Assert.That(_connectCommand.Executions[0].MenuId, Is.EqualTo("join"));
            Assert.That(_connectCommand.LastAddressValue, Is.EqualTo("10.0.0.5"));
        }

        [Test]
        public void Invoke_UnknownItemOrUnregisteredCommand_IsANoOp()
        {
            _menus.Open(_mainMenu);
            int before = _changedCount;

            _menus.Invoke("does-not-exist");
            _menus.Invoke("host"); // CommandId "Missing" is not registered

            Assert.That(_menus.Current!.MenuId, Is.EqualTo("main"));
            Assert.That(_changedCount, Is.EqualTo(before));
        }

        [Test]
        public void SetValue_SameValue_DoesNotRaiseChanged()
        {
            _menus.Open(_joinMenu);
            int before = _changedCount;

            _menus.SetValue("address", "127.0.0.1");

            Assert.That(_changedCount, Is.EqualTo(before));
        }

        [Test]
        public void Invoke_Toggle_FlipsValue()
        {
            _menus.Open(_mainMenu);

            _menus.Invoke("mute");

            Assert.That(_menus.GetValue("mute"), Is.EqualTo("True"));
            Assert.That(_menus.Current!.Items[2].Value, Is.EqualTo("True"));
        }

        [Test]
        public void ValuesSurviveLeavingAndReopeningAMenu()
        {
            _menus.Open(_mainMenu);
            _menus.Invoke("join");
            _menus.SetValue("address", "192.168.1.2");
            _menus.Back();

            _menus.Invoke("join");

            Assert.That(_menus.GetValue("address"), Is.EqualTo("192.168.1.2"));
        }

        [Test]
        public void CloseAll_ClearsTheWholeStack()
        {
            _menus.Open(_mainMenu);
            _menus.Invoke("join");

            _menus.CloseAll();

            Assert.That(_menus.IsOpen, Is.False);
            Assert.That(_menus.GetValue("address"), Is.Empty);
        }

        private MenuDefinition Track(MenuDefinition definition)
        {
            _createdAssets.Add(definition);
            return definition;
        }
    }
}
