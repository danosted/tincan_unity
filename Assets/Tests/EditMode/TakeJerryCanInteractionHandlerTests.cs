#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Features.Airship.Fuel;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.Tests.EditMode.Fakes;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class TakeJerryCanInteractionHandlerTests
    {
        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private sealed class PlainActor : IActor
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
        }

        private RecordingPublisher _events = null!;
        private TakeJerryCanInteractionHandler _handler = null!;
        private FakeJerryCanSupply _supply = null!;
        private FakeEquipmentActor _player = null!;
        private ItemDefinition _can = null!;
        private ItemDefinition _net = null!;

        [SetUp]
        public void SetUp()
        {
            _events = new RecordingPublisher();
            _handler = new TakeJerryCanInteractionHandler(_events);
            _can = ItemDefinition.Create(1, "ITEM_JerryCan");
            _net = ItemDefinition.Create(2, "ITEM_CatchingNet");
            _supply = new FakeJerryCanSupply { Count = 3, Item = _can };
            _player = new FakeEquipmentActor();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_can);
            Object.DestroyImmediate(_net);
        }

        [Test]
        public void Handle_EmptyHanded_TakesACan()
        {
            _handler.Handle(new InteractionContext(_player, _supply, null!));

            Assert.That(_player.Held, Is.SameAs(_can));
            Assert.That(_supply.Count, Is.EqualTo(2));
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<JerryCanTakenEvent>());
        }

        [Test]
        public void Handle_HoldingACan_ReturnsIt()
        {
            _player.Held = _can;

            _handler.Handle(new InteractionContext(_player, _supply, null!));

            Assert.That(_player.Held, Is.Null);
            Assert.That(_supply.Count, Is.EqualTo(4));
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<JerryCanReturnedEvent>());
        }

        [Test]
        public void Handle_SupplyEmpty_NothingHappens()
        {
            _supply.Count = 0;

            _handler.Handle(new InteractionContext(_player, _supply, null!));

            Assert.That(_player.Held, Is.Null);
            Assert.That(_supply.Count, Is.EqualTo(0));
        }

        [Test]
        public void Handle_HoldingSomethingElse_IsRefused()
        {
            _player.Held = _net;

            _handler.Handle(new InteractionContext(_player, _supply, null!));

            Assert.That(_player.Held, Is.SameAs(_net));
            Assert.That(_supply.Count, Is.EqualTo(3));
        }

        [Test]
        public void Handle_SupplyWithoutItem_HandsOutNothing()
        {
            _supply.Item = null;

            _handler.Handle(new InteractionContext(_player, _supply, null!));

            Assert.That(_player.Held, Is.Null);
            Assert.That(_supply.Count, Is.EqualTo(3));
        }

        [Test]
        public void Handle_RequesterWithoutEquipment_IsIgnored()
        {
            _handler.Handle(new InteractionContext(new PlainActor(), _supply, null!));

            Assert.That(_supply.Count, Is.EqualTo(3));
        }

        [Test]
        public void Handle_TargetIsNotASupply_IsIgnored()
        {
            Assert.DoesNotThrow(() => _handler.Handle(new InteractionContext(_player, new NotASupply(), null!)));
            Assert.That(_player.Held, Is.Null);
        }

        private sealed class NotASupply : IInteractable { }
    }
}
