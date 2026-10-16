#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Events;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Items;
using TinCan.Features.Respawn;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class OverboardUseCaseTests
    {
        private const ulong ClientId = 7;

        private RespawnRules _rules = null!;
        private GameplayTag _overboardTag = null!;
        private GameplayEffectDefinition _overboardEffect = null!;
        private ItemDefinition _hook = null!;
        private FakeActorRegistry _actors = null!;
        private FakeTimeService _time = null!;
        private RecordingPublisher _events = null!;
        private AbilitySystemUseCase _abilities = null!;
        private RecordingRespawn _respawn = null!;
        private FakeRequests _requests = null!;
        private LeashStore _leashes = null!;
        private FakeAirshipView _ship = null!;
        private FakeHumanoidMovementView _movement = null!;
        private Overboarder _player = null!;

        [SetUp]
        public void SetUp()
        {
            _overboardTag = ScriptableObject.CreateInstance<GameplayTag>();
            _overboardTag.name = "State.Overboard";
            _overboardEffect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _overboardEffect.DurationType = DurationType.Infinite;
            _overboardEffect.Modifiers = new List<AttributeModifier>();
            _overboardEffect.GrantedTags = new List<GameplayTag> { _overboardTag };
            _rules = ScriptableObject.CreateInstance<RespawnRules>();
            _rules.OverboardEffect = _overboardEffect;
            _rules.OverboardTag = _overboardTag;
            _rules.Rules = new List<RespawnRule> { new() { Cause = RespawnCause.Overboard, CarriedItem = CarriedItemOutcome.Lose, DelaySeconds = 0f } };
            _hook = ItemDefinition.Create(4, "ITEM_GrappleHook");

            _actors = new FakeActorRegistry();
            _time = new FakeTimeService { Time = 100f };
            _events = new RecordingPublisher();
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, _time, new FakeEventPublisher());
            _respawn = new RecordingRespawn();
            _requests = new FakeRequests();
            _leashes = new LeashStore();
            _actors.Register(_requests);

            _ship = new FakeAirshipView();
            _ship.Transform.position = new Vector3(0f, 40f, 0f);
            _actors.Register(_ship);

            _movement = new FakeHumanoidMovementView("Overboarder");
            _movement.Transform.position = new Vector3(20f, -15f, 0f);
            _player = new Overboarder(_movement) { Held = _hook };
            _player.AuthoritativeSetPossessor(ClientId);
            _actors.Register(_player);
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
            _ship.Destroy();
            Object.DestroyImmediate(_rules);
            Object.DestroyImmediate(_overboardEffect);
            Object.DestroyImmediate(_overboardTag);
            Object.DestroyImmediate(_hook);
            if (_leashedEffect != null) Object.DestroyImmediate(_leashedEffect);
            if (_leashedTag != null) Object.DestroyImmediate(_leashedTag);
            _leashedEffect = null;
            _leashedTag = null;
        }

        [Test]
        public void AFall_IsTaken_MarksThePlayer_Once()
        {
            var useCase = UseCase();

            Assert.That(useCase.TryHandleOverboard(_player), Is.True, "The cloud boundary must not teleport them.");
            Assert.That(useCase.TryHandleOverboard(_player), Is.True, "Still in hand on the next tick.");

            Assert.That(_abilities.TryGetActiveEffectGrantingTag(_player.Id, _overboardTag, out _), Is.True, "Marked overboard.");
            Assert.That(useCase.IsOverboard(_player.Id), Is.True);
            Assert.That(_events.Count<PlayerOverboardEvent>(), Is.EqualTo(1));
            Assert.That(_respawn.Calls, Is.Zero, "Nobody respawns before choosing.");
        }

        [Test]
        public void AFall_WithNoShip_OrOnAClient_OrWithoutTheEffect_IsLeftToTheTeleport()
        {
            _ship.IsSimulating = false;
            Assert.That(UseCase().TryHandleOverboard(_player), Is.False, "No ship: nothing to respawn aboard.");

            _ship.IsSimulating = true;
            Assert.That(UseCase(new FakeSessionNetworkService { IsServer = false, IsClient = true }).TryHandleOverboard(_player), Is.False);

            _rules.OverboardEffect = null;
            Assert.That(UseCase().TryHandleOverboard(_player), Is.False);
        }

        [Test]
        public void WithoutAChoice_ThePlayerWaits()
        {
            var useCase = UseCase();
            useCase.TryHandleOverboard(_player);

            _time.Time += 60f;
            useCase.Tick();

            Assert.That(_respawn.Calls, Is.Zero);
            Assert.That(useCase.IsOverboard(_player.Id), Is.True);
        }

        [Test]
        public void Choosing_RespawnsAboardTheNearestShip_ByTheRule()
        {
            var useCase = UseCase();
            useCase.TryHandleOverboard(_player);

            _requests.Pending.Add(new RespawnRequest(ClientId, OverboardChoiceKind.Respawn));
            useCase.Tick();

            Assert.That(_respawn.Calls, Is.EqualTo(1));
            Assert.That(_respawn.Position, Is.EqualTo(_ship.Transform.TransformPoint(_rules.BoardingOffset)), "On the ship's boarding pose.");
            Assert.That(_player.Held, Is.Null, "The rule says the carried item is lost.");
            Assert.That(_abilities.TryGetActiveEffectGrantingTag(_player.Id, _overboardTag, out _), Is.False, "No longer marked.");
            Assert.That(useCase.IsOverboard(_player.Id), Is.False);
            Assert.That(_events.Count<PlayerRespawnedEvent>(), Is.EqualTo(1));
        }

        [Test]
        public void ARuleThatKeepsTheItem_RespawnsHoldingIt()
        {
            _rules.Rules[0].CarriedItem = CarriedItemOutcome.Keep;
            var useCase = UseCase();
            useCase.TryHandleOverboard(_player);

            _requests.Pending.Add(new RespawnRequest(ClientId, OverboardChoiceKind.Respawn));
            useCase.Tick();

            Assert.That(_player.Held, Is.SameAs(_hook));
        }

        [Test]
        public void TheRulesDelay_HoldsTheRespawnBack_EvenAfterChoosing()
        {
            _rules.Rules[0].DelaySeconds = 5f;
            var useCase = UseCase();
            useCase.TryHandleOverboard(_player);
            _requests.Pending.Add(new RespawnRequest(ClientId, OverboardChoiceKind.Respawn));

            _time.Time += 2f;
            useCase.Tick();
            Assert.That(_respawn.Calls, Is.Zero, "3 s to go.");

            _time.Time += 3.5f;
            useCase.Tick();
            Assert.That(_respawn.Calls, Is.EqualTo(1));
        }

        [Test]
        public void AChoiceFromAnotherClient_DoesNothing()
        {
            var useCase = UseCase();
            useCase.TryHandleOverboard(_player);

            _requests.Pending.Add(new RespawnRequest(ClientId + 1, OverboardChoiceKind.Respawn));
            useCase.Tick();

            Assert.That(_respawn.Calls, Is.Zero);
        }

        [Test]
        public void AMarkedPlayer_IsHeldStill_ByTheHold()
        {
            var useCase = new OverboardHoldUseCase(_rules);
            var step = new HumanoidMotionStep(new Vector3(0f, -20f, 3f), Vector3.zero, 1f / 30f, isGrounded: false, isReplay: false);

            useCase.Contribute(_player, default, ref step);
            Assert.That(step.Velocity, Is.EqualTo(new Vector3(0f, -20f, 3f)), "Not overboard: untouched.");

            _player.HeldTags.Add(_overboardTag);
            useCase.Contribute(_player, default, ref step);
            Assert.That(step.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ChoosingTheLeash_HangsThemOnTheShipsPoint()
        {
            var point = AddLeashPoint();
            var useCase = UseCase();
            useCase.TryHandleOverboard(_player);

            _requests.Pending.Add(new RespawnRequest(ClientId, OverboardChoiceKind.Leash));
            useCase.Tick();

            Assert.That(useCase.IsLeashed(_player.Id), Is.True);
            Assert.That(_leashes.TryGet(_player.Id, out var leash) && ReferenceEquals(leash.Point, point), Is.True);
            Assert.That(leash.Length, Is.EqualTo(_rules.RopeLength));
            Assert.That(_abilities.TryGetActiveEffectGrantingTag(_player.Id, _leashedTag, out _), Is.True, "Marked leashed (server-driven).");
            Assert.That(_abilities.TryGetActiveEffectGrantingTag(_player.Id, _overboardTag, out _), Is.False, "No longer held still overboard.");
            Assert.That(_respawn.Calls, Is.Zero);
        }

        [Test]
        public void WithoutALeashPoint_TheLeashIsRefused_AndTheyStayOverboard()
        {
            var useCase = UseCase();
            useCase.TryHandleOverboard(_player);

            _requests.Pending.Add(new RespawnRequest(ClientId, OverboardChoiceKind.Leash));
            useCase.Tick();

            Assert.That(useCase.IsLeashed(_player.Id), Is.False);
            Assert.That(useCase.IsOverboard(_player.Id), Is.True, "Respawning is still open.");
        }

        [Test]
        public void ReachingThePoint_LandsThemOnDeck_KeepingWhatTheyCarry()
        {
            var point = AddLeashPoint();
            var useCase = UseCase();
            useCase.TryHandleOverboard(_player);
            _requests.Pending.Add(new RespawnRequest(ClientId, OverboardChoiceKind.Leash));
            useCase.Tick();

            _leashes.TryGet(_player.Id, out var leash);
            leash.Arrived = true;
            useCase.Tick();

            Assert.That(_respawn.Calls, Is.EqualTo(1));
            Assert.That(_respawn.Position, Is.EqualTo(point.Stand.position), "On deck at the point's stand spot.");
            Assert.That(_player.Held, Is.SameAs(_hook), "A rescue costs nothing.");
            Assert.That(useCase.IsOverboard(_player.Id), Is.False);
            Assert.That(_leashes.TryGet(_player.Id, out _), Is.False);
            Assert.That(_abilities.TryGetActiveEffectGrantingTag(_player.Id, _leashedTag, out _), Is.False);
            Assert.That(_events.Count<PlayerRescuedEvent>(), Is.EqualTo(1));
        }

        [Test]
        public void RespawningWhileLeashed_LetsGoOfTheLeash_ByTheRule()
        {
            AddLeashPoint();
            var useCase = UseCase();
            useCase.TryHandleOverboard(_player);
            _requests.Pending.Add(new RespawnRequest(ClientId, OverboardChoiceKind.Leash));
            useCase.Tick();

            _requests.Pending.Add(new RespawnRequest(ClientId, OverboardChoiceKind.Respawn));
            useCase.Tick();

            Assert.That(_respawn.Position, Is.EqualTo(_ship.Transform.TransformPoint(_rules.BoardingOffset)));
            Assert.That(_player.Held, Is.Null, "The respawn rule applies, leash or not.");
            Assert.That(_leashes.TryGet(_player.Id, out _), Is.False);
        }

        private FakeLeashPoint AddLeashPoint()
        {
            _leashedTag = ScriptableObject.CreateInstance<GameplayTag>();
            _leashedEffect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _leashedEffect.DurationType = DurationType.Infinite;
            _leashedEffect.Modifiers = new List<AttributeModifier>();
            _leashedEffect.GrantedTags = new List<GameplayTag> { _leashedTag };
            _rules.LeashedEffect = _leashedEffect;
            _rules.LeashedTag = _leashedTag;

            var point = new FakeLeashPoint(_ship.Transform);
            _actors.Register(point);
            return point;
        }

        private GameplayTag? _leashedTag;
        private GameplayEffectDefinition? _leashedEffect;

        /// <summary>A leash point on the ship: the anchor outside the rail, the stand spot on deck.</summary>
        private sealed class FakeLeashPoint : ILeashPoint
        {
            private readonly GameObject _root;

            public FakeLeashPoint(Transform ship)
            {
                _root = new GameObject("LeashPoint");
                _root.transform.SetParent(ship, false);
                Anchor = new GameObject("Anchor").transform;
                Anchor.SetParent(_root.transform, false);
                Anchor.localPosition = new Vector3(6.5f, -2.5f, 0f);
                Stand = new GameObject("Stand").transform;
                Stand.SetParent(_root.transform, false);
                Stand.localPosition = new Vector3(4.5f, -2.4f, 0f);
                Ship = ship;
            }

            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public Transform Anchor { get; }
            public Transform Stand { get; }
            public Transform? Ship { get; }
        }

        private OverboardUseCase UseCase(TinCan.Core.Domain.Networking.INetworkService? network = null) =>
            new(_rules, _leashes, network ?? new FakeNetworkService(), _actors, _time, _events, _abilities, _respawn);

        /// <summary>A player who is also their own equipment, as the player actor is in the game.</summary>
        private sealed class Overboarder : FakeHumanoidCharacterView, IEquipment
        {
            public Overboarder(FakeHumanoidMovementView movement) : base(movement) { }

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

        private sealed class FakeRequests : IRespawnRequests
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public List<RespawnRequest> Pending { get; } = new();
            public void Request(OverboardChoiceKind choice) { }

            public IReadOnlyList<RespawnRequest> TakeRequests()
            {
                var taken = Pending.ToArray();
                Pending.Clear();
                return taken;
            }
        }

        private sealed class RecordingRespawn : IHumanoidRespawnService
        {
            public int Calls { get; private set; }
            public Vector3 Position { get; private set; }

            public void ResetCharacter(IHumanoidCharacterView character, Vector3 position, Quaternion rotation)
            {
                Calls++;
                Position = position;
                character.Movement.SetPose(position, rotation);
            }
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            private readonly List<object> _events = new();
            public int Count<T>() => _events.FindAll(e => e is T).Count;
            public void Publish<TEvent>(TEvent evt) => _events.Add(evt!);
        }
    }
}
