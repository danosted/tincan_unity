#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas.Inputs;
using TinCan.Core.Humanoid;
using TinCan.Core.Items;
using TinCan.Features.Camping.Tests.Fakes;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Camping.Tests
{
    /// <summary><see cref="CampPlacementUseCase"/>: a press ties a log, or sets a bundle or tent roll down where it fits.</summary>
    public class CampPlacementUseCaseTests
    {
        private const int PlaceBit = 2;

        /// <summary>A player whose body is also its equipment.</summary>
        private sealed class Holder : FakeHumanoidCharacterView, IEquipment
        {
            public Holder(FakeHumanoidMovementView movement) : base(movement) { }
            public ItemDefinition? Held { get; set; }

            public bool TryEquip(ItemDefinition item)
            {
                if (Held != null) return false;
                Held = item;
                return true;
            }

            public bool TryUnequip()
            {
                if (Held == null) return false;
                Held = null;
                return true;
            }
        }

        private CampingTestKit _kit = null!;
        private ItemDefinition _bundle = null!;
        private GameplayTag _carryingFirewood = null!, _carryingBundle = null!, _carryingTent = null!;
        private PrimaryInput _place = null!;
        private FakeCampPlacementQuery _query = null!;
        private FakeCampSpawner _spawner = null!;
        private FakeActorRegistry _actors = null!;
        private FakeHumanoidMovementView _movement = null!;
        private Holder _player = null!;
        private uint _sequence;

        [SetUp]
        public void SetUp()
        {
            _kit = new CampingTestKit();
            _bundle = ItemDefinition.Create(7, "ITEM_CampfireBundle");
            _carryingFirewood = ScriptableObject.CreateInstance<GameplayTag>();
            _carryingBundle = ScriptableObject.CreateInstance<GameplayTag>();
            _carryingTent = ScriptableObject.CreateInstance<GameplayTag>();
            _place = ScriptableObject.CreateInstance<PrimaryInput>();
            _place.BitIndex = PlaceBit;

            var config = _kit.Config;
            config.BundleItem = _bundle;
            config.PlaceInput = _place;
            config.CarryingFirewoodTag = _carryingFirewood;
            config.CarryingBundleTag = _carryingBundle;
            config.CarryingTentTag = _carryingTent;

            _query = new FakeCampPlacementQuery();
            _spawner = new FakeCampSpawner();
            _actors = new FakeActorRegistry();
            _movement = new FakeHumanoidMovementView("Camper");
            _player = new Holder(_movement);
            _actors.Register(_player);
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
            _kit.Dispose();
            Object.DestroyImmediate(_bundle);
            Object.DestroyImmediate(_carryingFirewood);
            Object.DestroyImmediate(_carryingBundle);
            Object.DestroyImmediate(_carryingTent);
            Object.DestroyImmediate(_place);
        }

        private CampPlacementUseCase Create() =>
            new(new FakeNetworkService(), _actors, new CampPlacementFinder(_query, _kit.Config), _spawner, _kit.Config, _kit.Events);

        private void Hold(ItemDefinition item, GameplayTag carrying)
        {
            _player.Held = item;
            _player.HeldTags.Clear();
            _player.HeldTags.Add(carrying);
        }

        /// <summary>One movement step with the place input held or not, then the tick after movement.</summary>
        private void Step(CampPlacementUseCase useCase, bool press, bool replay = false)
        {
            var input = new HumanoidInputState
            {
                Sequence = ++_sequence,
                LookRotation = Quaternion.identity,
                LookPitch = 40f,
                ActiveInputMask = press ? 1UL << PlaceBit : 0UL
            };
            var step = new HumanoidMotionStep(Vector3.zero, Vector3.zero, 0.02f, isGrounded: true, isReplay: replay);
            useCase.Contribute(_player, input, ref step);
            useCase.Tick();
        }

        [Test]
        public void HoldingALog_APress_TiesItIntoABundle()
        {
            Hold(_kit.Firewood, _carryingFirewood);
            var useCase = Create();

            Step(useCase, press: true);

            Assert.That(_player.Held, Is.SameAs(_bundle));
            Assert.That(_kit.Events.Events.OfType<CampfireBundledEvent>().Count(), Is.EqualTo(1));
            Assert.That(_spawner.Spawned, Is.Empty);
        }

        [Test]
        public void HoldingABundle_APressWhereItFits_SetsAnUnlitCampfireDown()
        {
            Hold(_bundle, _carryingBundle);
            var useCase = Create();

            Step(useCase, press: true);

            Assert.That(_spawner.Spawned.Select(s => s.Kind), Is.EqualTo(new[] { CampPlaceable.Campfire }));
            Assert.That(_spawner.Spawned[0].Position, Is.EqualTo(_query.Ground!.Value.Point));
            Assert.That(_player.Held, Is.Null, "the bundle is used up");
            Assert.That(_kit.Events.Events.OfType<CampPlacedEvent>().Single().Kind, Is.EqualTo(CampPlaceable.Campfire));
        }

        [Test]
        public void HoldingATentRoll_APress_SetsATentDown()
        {
            Hold(_kit.Tent, _carryingTent);
            var useCase = Create();

            Step(useCase, press: true);

            Assert.That(_spawner.Spawned.Select(s => s.Kind), Is.EqualTo(new[] { CampPlaceable.Tent }));
            Assert.That(_player.Held, Is.Null);
        }

        [Test]
        public void WhereItDoesNotFit_NothingIsPlaced_AndTheItemIsKept()
        {
            Hold(_bundle, _carryingBundle);
            _query.Room = false;
            var useCase = Create();

            Step(useCase, press: true);

            Assert.That(_spawner.Spawned, Is.Empty);
            Assert.That(_player.Held, Is.SameAs(_bundle));
        }

        [Test]
        public void OnTheShip_NothingIsPlaced()
        {
            Hold(_bundle, _carryingBundle);
            _query.Ground = new CampGround(Vector3.forward, Vector3.up, onShip: true);
            var useCase = Create();

            Step(useCase, press: true);

            Assert.That(_spawner.Spawned, Is.Empty);
        }

        [Test]
        public void HoldingThePress_PlacesOnce_UntilItIsLetGo()
        {
            Hold(_kit.Tent, _carryingTent);
            var useCase = Create();

            Step(useCase, press: true);
            Hold(_kit.Tent, _carryingTent);
            Step(useCase, press: true);
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(1), "held, not pressed again");

            Step(useCase, press: false);
            Step(useCase, press: true);
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(2));
        }

        [Test]
        public void AFailedSpawn_KeepsTheItem()
        {
            Hold(_bundle, _carryingBundle);
            _spawner.Fails = true;

            Step(Create(), press: true);

            Assert.That(_player.Held, Is.SameAs(_bundle));
        }

        [Test]
        public void AnInputSeenAgain_DoesNotPressTwice()
        {
            Hold(_kit.Tent, _carryingTent);
            var useCase = Create();
            Step(useCase, press: false);
            Step(useCase, press: true);

            _sequence -= 2;
            Hold(_kit.Tent, _carryingTent);
            Step(useCase, press: false, replay: true);
            Step(useCase, press: true, replay: true);

            Assert.That(_spawner.Spawned, Has.Count.EqualTo(1));
        }

        [Test]
        public void AReleaseSeenOnlyAsAReplay_StillLetsTheNextPressThrough()
        {
            Hold(_kit.Tent, _carryingTent);
            var useCase = Create();
            Step(useCase, press: true);

            Hold(_kit.Tent, _carryingTent);
            Step(useCase, press: false, replay: true);
            Step(useCase, press: true);

            Assert.That(_spawner.Spawned, Has.Count.EqualTo(2));
        }

        [Test]
        public void EmptyHanded_APress_DoesNothing()
        {
            Step(Create(), press: true);

            Assert.That(_spawner.Spawned, Is.Empty);
            Assert.That(_player.Held, Is.Null);
        }
    }
}
