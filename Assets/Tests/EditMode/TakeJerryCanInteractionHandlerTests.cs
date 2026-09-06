#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Carry;
using TinCan.Features.Interaction;
using TinCan.Tests.EditMode.Fakes;

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
        private FakeCarrierActor _player = null!;

        [SetUp]
        public void SetUp()
        {
            _events = new RecordingPublisher();
            _handler = new TakeJerryCanInteractionHandler(_events);
            _supply = new FakeJerryCanSupply { Count = 3 };
            _player = new FakeCarrierActor();
        }

        [Test]
        public void Handle_EmptyHanded_TakesACan()
        {
            _handler.Handle(new InteractionContext(_player, _supply, null!));

            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.JerryCan));
            Assert.That(_supply.Count, Is.EqualTo(2));
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<JerryCanTakenEvent>());
        }

        [Test]
        public void Handle_CarryingACan_ReturnsIt()
        {
            _player.Carried = CarriedItem.JerryCan;

            _handler.Handle(new InteractionContext(_player, _supply, null!));

            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.None));
            Assert.That(_supply.Count, Is.EqualTo(4));
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<JerryCanReturnedEvent>());
        }

        [Test]
        public void Handle_SupplyEmpty_NothingHappens()
        {
            _supply.Count = 0;

            _handler.Handle(new InteractionContext(_player, _supply, null!));

            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.None));
            Assert.That(_supply.Count, Is.EqualTo(0));
        }

        [Test]
        public void Handle_CarryingSomethingElse_IsRefused()
        {
            _player.Carried = CarriedItem.Net;

            _handler.Handle(new InteractionContext(_player, _supply, null!));

            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.Net));
            Assert.That(_supply.Count, Is.EqualTo(3));
        }

        [Test]
        public void Handle_RequesterWithoutCarrier_IsIgnored()
        {
            _handler.Handle(new InteractionContext(new PlainActor(), _supply, null!));

            Assert.That(_supply.Count, Is.EqualTo(3));
        }

        [Test]
        public void Handle_TargetIsNotASupply_IsIgnored()
        {
            var tank = new FakeFuelTank();
            var port = new NotASupply();

            Assert.DoesNotThrow(() => _handler.Handle(new InteractionContext(_player, port, null!)));
            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.None));
        }

        private sealed class NotASupply : IInteractable { }
    }
}
