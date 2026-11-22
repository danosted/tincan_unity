#nullable enable
using NUnit.Framework;
using TinCan.Core.Interaction;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Core.Items.Tests
{
    /// <summary>A stockpile (a crystal bin) through <see cref="StockItemInteractionHandler"/>, and the HUD's held-item line (<see cref="HeldItemPresenter"/>).</summary>
    public class StockItemInteractionHandlerTests
    {
        private sealed class FakeStore : IItemStockpile
        {
            public ItemDefinition? Item { get; set; }
            public int Count { get; set; }
            public int Capacity { get; set; } = 3;

            public bool ServerTryAdd()
            {
                if (Count >= Capacity) return false;
                Count++;
                return true;
            }

            public bool ServerTryTake()
            {
                if (Count <= 0) return false;
                Count--;
                return true;
            }
        }

        private StockItemInteractionHandler _handler = null!;
        private FakeEquipmentActor _player = null!;
        private ItemDefinition _crystal = null!;
        private ItemDefinition _log = null!;
        private FakeStore _bin = null!;

        [SetUp]
        public void SetUp()
        {
            _handler = new StockItemInteractionHandler(new FakeEventPublisher());
            _player = new FakeEquipmentActor();
            _crystal = ItemDefinition.Create(8, "ITEM_FuelCrystal");
            _log = ItemDefinition.Create(6, "ITEM_Firewood");
            _bin = new FakeStore { Item = _crystal };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_crystal);
            Object.DestroyImmediate(_log);
        }

        private void Interact() => _handler.Handle(new InteractionContext(_player, _bin, null!));

        [Test]
        public void HoldingItsItem_PutsItIn()
        {
            _player.Held = _crystal;

            Interact();

            Assert.That(_bin.Count, Is.EqualTo(1));
            Assert.That(_player.Held, Is.Null);
        }

        [Test]
        public void EmptyHanded_TakesOneOut()
        {
            _bin.Count = 2;

            Interact();

            Assert.That(_bin.Count, Is.EqualTo(1));
            Assert.That(_player.Held, Is.SameAs(_crystal));
        }

        [Test]
        public void AFullStore_KeepsTheItemInHand()
        {
            _bin.Count = _bin.Capacity;
            _player.Held = _crystal;

            Interact();

            Assert.That(_bin.Count, Is.EqualTo(_bin.Capacity));
            Assert.That(_player.Held, Is.SameAs(_crystal));
        }

        [Test]
        public void AnEmptyStore_GivesNothing()
        {
            Interact();

            Assert.That(_player.Held, Is.Null);
            Assert.That(_bin.Count, Is.Zero);
        }

        [Test]
        public void AnotherItemInHand_IsRefused()
        {
            _bin.Count = 1;
            _player.Held = _log;

            Interact();

            Assert.That(_player.Held, Is.SameAs(_log));
            Assert.That(_bin.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheHud_NamesWhatIsHeld_AndClearsWhenEmptyHanded()
        {
            var hud = new FakeHudValues();
            var presenter = new HeldItemPresenter(new FakeActorRegistry(), hud);

            presenter.Step(_crystal);
            Assert.That(hud.All[HeldItemPresenter.Key], Is.EqualTo(_crystal.DisplayName));

            presenter.Step(null);
            Assert.That(hud.All.ContainsKey(HeldItemPresenter.Key), Is.False);
        }
    }
}
