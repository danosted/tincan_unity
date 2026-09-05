#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Carry;
using TinCan.Features.Interaction;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class PourFuelInteractionHandlerTests
    {
        private sealed class FakeFillPort : IInteractable, IFuelFillPort
        {
            public IFuelTank? Tank { get; set; }
        }

        private sealed class PlainActor : IActor
        {
            public System.Guid Id { get; } = System.Guid.NewGuid();
            public bool IsSimulating => true;
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private RecordingPublisher _events = null!;
        private PourFuelInteractionHandler _handler = null!;
        private FuelConfig _config = null!;
        private FakeFuelTank _tank = null!;
        private FakeFillPort _port = null!;
        private FakeCarrierActor _player = null!;

        [SetUp]
        public void SetUp()
        {
            _events = new RecordingPublisher();
            _handler = new PourFuelInteractionHandler(_events);
            _config = ScriptableObject.CreateInstance<FuelConfig>();
            _config.JerryCanLitres = 25f;
            _config.DebugFreeRefuel = true;
            _tank = new FakeFuelTank { Level = 50f, Capacity = 100f, Config = _config };
            _port = new FakeFillPort { Tank = _tank };
            _player = new FakeCarrierActor();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void Handle_DebugRefuelEnabled_EmptyHanded_AddsOneJerryCan()
        {
            _handler.Handle(Context(_player, _port));

            Assert.That(_tank.Level, Is.EqualTo(75f));
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<FuelRefilledEvent>());
        }

        [Test]
        public void Handle_DebugRefuelDisabled_EmptyHanded_IsANoOp()
        {
            _config.DebugFreeRefuel = false;

            _handler.Handle(Context(_player, _port));

            Assert.That(_tank.Level, Is.EqualTo(50f));
            Assert.That(_tank.RefillCalls, Is.EqualTo(0));
        }

        [Test]
        public void Handle_CarryingACan_PoursItAndEmptiesHands()
        {
            _config.DebugFreeRefuel = false;
            _player.Carried = CarriedItem.JerryCan;

            _handler.Handle(Context(_player, _port));

            Assert.That(_tank.Level, Is.EqualTo(75f));
            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.None));
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<FuelRefilledEvent>());
        }

        [Test]
        public void Handle_CarryingACanIntoAFullTank_KeepsTheCan()
        {
            _tank.Level = 100f;
            _player.Carried = CarriedItem.JerryCan;

            _handler.Handle(Context(_player, _port));

            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.JerryCan));
            Assert.That(_events.Events, Has.None.TypeOf<FuelRefilledEvent>());
        }

        [Test]
        public void Handle_CarryingSomethingElse_DoesNotRefuelEvenWithDebugOn()
        {
            _player.Carried = CarriedItem.Net;

            _handler.Handle(Context(_player, _port));

            Assert.That(_tank.Level, Is.EqualTo(50f));
            Assert.That(_player.Carried, Is.EqualTo(CarriedItem.Net));
        }

        [Test]
        public void Handle_RequesterWithoutCarrier_UsesDebugPathOnly()
        {
            _handler.Handle(Context(new PlainActor(), _port));
            Assert.That(_tank.Level, Is.EqualTo(75f));

            _config.DebugFreeRefuel = false;
            _handler.Handle(Context(new PlainActor(), _port));
            Assert.That(_tank.Level, Is.EqualTo(75f));
        }

        [Test]
        public void Handle_ClampsAtCapacity()
        {
            _tank.Level = 90f;

            _handler.Handle(Context(_player, _port));

            Assert.That(_tank.Level, Is.EqualTo(100f));
        }

        [Test]
        public void Handle_TargetIsNotAFillPort_IsIgnored()
        {
            Assert.DoesNotThrow(() => _handler.Handle(Context(_player, new NotAPort())));
            Assert.That(_tank.RefillCalls, Is.EqualTo(0));
        }

        private static InteractionContext Context(IActor requester, IInteractable target) => new(requester, target, null!);

        private sealed class NotAPort : IInteractable { }
    }
}
