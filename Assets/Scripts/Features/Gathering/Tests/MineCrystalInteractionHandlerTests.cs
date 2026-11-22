#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Gathering.Tests
{
    /// <summary>Mining a vein (<see cref="MineCrystalInteractionHandler"/>, run when the held work is done).</summary>
    public class MineCrystalInteractionHandlerTests
    {
        private sealed class FakeVein : IVein
        {
            public GatheringConfig? Config { get; set; }
            public int Available { get; set; }

            public bool TryTake()
            {
                if (Available <= 0) return false;
                Available--;
                return true;
            }

            public void Return() => Available++;
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private ItemDefinition _crystal = null!;
        private ItemDefinition _other = null!;
        private GatheringConfig _config = null!;
        private RecordingPublisher _events = null!;
        private FakeVein _vein = null!;
        private FakeEquipmentActor _player = null!;
        private MineCrystalInteractionHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _crystal = ItemDefinition.Create(41, "ITEM_FuelCrystal");
            _other = ItemDefinition.Create(42, "ITEM_Other");
            _config = ScriptableObject.CreateInstance<GatheringConfig>();
            _config.CrystalItem = _crystal;
            _events = new RecordingPublisher();
            _vein = new FakeVein { Config = _config, Available = 3 };
            _player = new FakeEquipmentActor();
            _handler = new MineCrystalInteractionHandler(_events);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_crystal);
            Object.DestroyImmediate(_other);
            Object.DestroyImmediate(_config);
        }

        private void Mine() => _handler.Handle(new InteractionContext(_player, _vein, null!));

        [Test]
        public void EmptyHanded_ACrystalBreaksOffIntoYourHands()
        {
            Mine();

            Assert.That(_player.Held, Is.SameAs(_crystal));
            Assert.That(_vein.Available, Is.EqualTo(2));
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<CrystalMinedEvent>());
        }

        [Test]
        public void ABareVein_GivesNothing()
        {
            _vein.Available = 0;

            Mine();

            Assert.That(_player.Held, Is.Null);
            Assert.That(_events.Events, Has.None.TypeOf<CrystalMinedEvent>());
        }

        [Test]
        public void HandsFull_CannotMine_AndTheVeinKeepsItsCrystal()
        {
            _player.Held = _other;

            Mine();

            Assert.That(_player.Held, Is.SameAs(_other));
            Assert.That(_vein.Available, Is.EqualTo(3));
        }

        [Test]
        public void HoldingACrystal_CannotMineAnother()
        {
            _player.Held = _crystal;

            Mine();

            Assert.That(_vein.Available, Is.EqualTo(3), "one crystal per trip");
        }
    }
}
