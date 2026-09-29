#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Fuel.Minigame;
using TinCan.Features.Carry;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class TakeNetInteractionHandlerTests
    {
        private TakeNetInteractionHandler _handler = null!;
        private FlyingCanConfig _config = null!;
        private FakeNetRack _rack = null!;
        private FakeEquipmentActor _player = null!;
        private ItemDefinition _net = null!;
        private ItemDefinition _can = null!;

        [SetUp]
        public void SetUp()
        {
            _net = ItemDefinition.Create(2, "ITEM_CatchingNet");
            _can = ItemDefinition.Create(1, "ITEM_JerryCan");
            _config = ScriptableObject.CreateInstance<FlyingCanConfig>();
            _config.NetItem = _net;
            _handler = new TakeNetInteractionHandler(new FakeEventPublisher(), _config);
            _rack = new FakeNetRack();
            _player = new FakeEquipmentActor();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_net);
            Object.DestroyImmediate(_can);
        }

        [Test]
        public void Handle_EmptyHanded_TakesNet()
        {
            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Held, Is.SameAs(_net));
        }

        [Test]
        public void Handle_HoldingNet_ReturnsIt()
        {
            _player.Held = _net;

            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Held, Is.Null);
        }

        [Test]
        public void Handle_HoldingJerryCan_IsRefused()
        {
            _player.Held = _can;

            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Held, Is.SameAs(_can));
        }

        [Test]
        public void Handle_ConfigWithoutNetItem_HandsOutNothing()
        {
            _config.NetItem = null;

            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Held, Is.Null);
        }

        [Test]
        public void Handle_TargetNotARack_IsIgnored()
        {
            _handler.Handle(new InteractionContext(_player, new FakeJerryCanSupply(), null!));

            Assert.That(_player.Held, Is.Null);
        }
    }
}
