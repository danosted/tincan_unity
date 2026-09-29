#nullable enable
using NUnit.Framework;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class TakeItemInteractionHandlerTests
    {
        private sealed class FakeRack : IItemSource
        {
            public ItemDefinition? Item { get; set; }
        }

        private TakeItemInteractionHandler _handler = null!;
        private FakeEquipmentActor _player = null!;
        private ItemDefinition _tool = null!;
        private ItemDefinition _net = null!;
        private FakeRack _rack = null!;

        [SetUp]
        public void SetUp()
        {
            _handler = new TakeItemInteractionHandler(new FakeEventPublisher());
            _player = new FakeEquipmentActor();
            _tool = ItemDefinition.Create(3, "ITEM_RepairTool");
            _net = ItemDefinition.Create(2, "ITEM_CatchingNet");
            _rack = new FakeRack { Item = _tool };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tool);
            Object.DestroyImmediate(_net);
        }

        [Test]
        public void EmptyHanded_TakesTheRacksItem()
        {
            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Held, Is.SameAs(_tool));
        }

        [Test]
        public void HoldingTheRacksItem_PutsItBack()
        {
            _player.Held = _tool;

            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Held, Is.Null);
        }

        [Test]
        public void HoldingSomethingElse_IsRefused()
        {
            _player.Held = _net;

            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Held, Is.SameAs(_net));
        }

        [Test]
        public void RackWithoutItem_HandsOutNothing()
        {
            _rack.Item = null;

            _handler.Handle(new InteractionContext(_player, _rack, null!));

            Assert.That(_player.Held, Is.Null);
        }

        [Test]
        public void TargetIsNotARack_IsIgnored()
        {
            _handler.Handle(new InteractionContext(_player, new FakeNetRack(), null!));

            Assert.That(_player.Held, Is.Null);
        }
    }
}
