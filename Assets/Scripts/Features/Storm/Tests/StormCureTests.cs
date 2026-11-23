#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Events;
using TinCan.Features.Encounters;
using TinCan.Tests.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Features.Storm.Tests
{
    /// <summary>
    /// The storm's heart and its cure (storm-crossing.md, ST5): the heart's path by severity, the target rules, where a
    /// beam meets a target, and <see cref="StormCureUseCase"/>: the round's reset, hits on the live target, the cure.
    /// </summary>
    public class StormCureTests
    {
        private sealed class Zone : IEncounterZone
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public string DisplayName => "Storm";
            public Vector2 Centre { get; set; } = new(0f, 100f);
            public float Radius => 750f;
            public EncounterZoneState State { get; set; } = EncounterZoneState.Running;
            public double StateEndsAt { get; private set; }
            public void ServerSetCentre(Vector2 centre) => Centre = centre;

            public void ServerSetState(EncounterZoneState state, double endsAt)
            {
                State = state;
                StateEndsAt = endsAt;
            }
        }

        private sealed class Heart : IStormHeart
        {
            public float Strength { get; set; } = 1f;
            public float Altitude { get; set; } = 30f;
            public long HitSlot { get; set; } = -1;
            public int Resets { get; private set; }

            public void ServerReset(float altitude)
            {
                Resets++;
                (Strength, HitSlot, Altitude) = (1f, -1, altitude);
            }

            public void ServerHit(long slot, float strength) => (HitSlot, Strength) = (slot, strength);
        }

        private sealed class Events : IEventPublisher
        {
            public readonly List<object> Published = new();
            public void Publish<TEvent>(TEvent evt) => Published.Add(evt!);
        }

        private readonly List<Object> _assets = new();
        private StormConfig _config = null!;
        private EncounterZoneConfig _zoneConfig = null!;
        private FakeTimeService _time = null!;
        private FakeActorRegistry _actors = null!;
        private Events _events = null!;
        private Zone _zone = null!;
        private Heart _heart = null!;
        private StormCureUseCase _cure = null!;

        [SetUp]
        public void SetUp()
        {
            _config = Create<StormConfig>();
            _zoneConfig = Create<EncounterZoneConfig>();
            _time = new FakeTimeService { Time = 1f };
            _actors = new FakeActorRegistry();
            _events = new Events();
            _zone = new Zone();
            _heart = new Heart();
            _actors.Register(_zone);
            _cure = new StormCureUseCase(new FakeNetworkService(), _actors, _time, _events, _config, _zoneConfig,
                zone => ReferenceEquals(zone, _zone) ? _heart : null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Heart_IsStillAtSeverityOne_AndWandersFartherInHarderStorms()
        {
            var centre = new Vector2(5f, 7f);
            var still = new Vector3(5f, 30f, 7f);

            Assert.That(StormHeartProcessor.Heart(centre, 30f, 1, 4, 0d, _config), Is.EqualTo(still));
            Assert.That(StormHeartProcessor.Heart(centre, 30f, 1, 4, 37d, _config), Is.EqualTo(still));

            float FarthestAt(int severity)
            {
                float farthest = 0f;
                for (int second = 0; second < 120; second++)
                    farthest = Mathf.Max(farthest, Vector3.Distance(StormHeartProcessor.Heart(centre, 30f, severity, 4, second, _config), still));
                return farthest;
            }

            Assert.That(FarthestAt(2), Is.GreaterThan(1f).And.LessThanOrEqualTo(_config.HeartWander * 1.1f), "a figure of eight, about its radius out");
            Assert.That(FarthestAt(3), Is.GreaterThan(FarthestAt(2)));
        }

        [Test]
        public void Heart_IsTheSameFromTheSameSeedAndTime()
        {
            var a = StormHeartProcessor.Heart(Vector2.zero, 0f, 3, 9, 12.5d, _config);
            var b = StormHeartProcessor.Heart(Vector2.zero, 0f, 3, 9, 12.5d, _config);

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void Targets_StayUpForTheirSeconds_ThenGo_AndHarderStormsAskMore()
        {
            Assert.That(StormHeartProcessor.TargetUp(0.1d, 1, _config), Is.True);
            Assert.That(StormHeartProcessor.TargetUp(_config.TargetSeconds + 0.1d, 1, _config), Is.False, "the gap between targets");
            Assert.That(StormHeartProcessor.Slot(_config.TargetSeconds + _config.TargetGapSeconds + 0.1d, 1, _config), Is.EqualTo(1L));

            Assert.That(StormHeartProcessor.TargetSeconds(3, _config), Is.LessThan(StormHeartProcessor.TargetSeconds(1, _config)));
            Assert.That(StormHeartProcessor.HitsToCure(3, _config), Is.GreaterThan(StormHeartProcessor.HitsToCure(1, _config)));
        }

        [Test]
        public void TargetOffset_StaysOnTheRing_AndDiffersBySlot()
        {
            for (long slot = 0; slot < 20; slot++)
            {
                var offset = StormHeartProcessor.TargetOffset(slot, 3, _config);
                float across = new Vector2(offset.x, offset.z).magnitude;
                Assert.That(across, Is.InRange(_config.TargetRing.x - 1e-3f, _config.TargetRing.y + 1e-3f));
                Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(_config.TargetHeight + 1e-3f));
            }

            Assert.That(StormHeartProcessor.TargetOffset(1, 3, _config), Is.Not.EqualTo(StormHeartProcessor.TargetOffset(2, 3, _config)));
        }

        [Test]
        public void LiveTarget_IsGoneOnceItsSlotIsHit()
        {
            Assert.That(StormHeartProcessor.LiveTarget(Vector2.zero, 0f, 1, 1, 0.5d, -1, _config), Is.Not.Null);
            Assert.That(StormHeartProcessor.LiveTarget(Vector2.zero, 0f, 1, 1, 0.5d, 0, _config), Is.Null);
        }

        [Test]
        public void Enters_TheNearSideOfTheSphere_OrMisses()
        {
            var centre = new Vector3(0f, 0f, 10f);

            Assert.That(StormHeartProcessor.Enters(Vector3.zero, Vector3.forward, 100f, centre, 2f), Is.EqualTo(8f).Within(1e-3f));
            Assert.That(StormHeartProcessor.Enters(new Vector3(3f, 0f, 0f), Vector3.forward, 100f, centre, 2f), Is.Null, "passes beside it");
            Assert.That(StormHeartProcessor.Enters(Vector3.zero, Vector3.forward, 5f, centre, 2f), Is.Null, "stops short of it");
            Assert.That(StormHeartProcessor.Enters(Vector3.zero, Vector3.back, 100f, centre, 2f), Is.Null, "behind the muzzle");
            Assert.That(StormHeartProcessor.Enters(new Vector3(0f, 0f, 9f), Vector3.forward, 100f, centre, 2f), Is.EqualTo(0f), "starts inside it");
        }

        [Test]
        public void Warning_SetsTheHeartRaging_AboveTheShipSailingIn()
        {
            var ship = new FakeAirshipView();
            ship.Transform.position = new Vector3(0f, 42f, 50f);
            _actors.Register(ship);
            _zone.State = EncounterZoneState.Warning;
            _heart.Strength = 0f;
            _heart.HitSlot = 7;

            try
            {
                _cure.Tick();

                Assert.That(_heart.Strength, Is.EqualTo(1f));
                Assert.That(_heart.HitSlot, Is.EqualTo(-1L));
                Assert.That(_heart.Altitude, Is.EqualTo(42f + _config.HeartAboveShip));
            }
            finally
            {
                ship.Destroy();
            }
        }

        [Test]
        public void Beam_ThroughTheLiveTarget_HitsIt_Once()
        {
            Vector3 target = LiveTarget();
            Vector3 origin = target + Vector3.back * 40f;

            Assert.That(_cure.FirstHit(origin, Vector3.forward, 100f), Is.EqualTo(40f - _config.TargetRadius).Within(1e-3f));
            _cure.Answer(origin, Vector3.forward, 40f - _config.TargetRadius);

            Assert.That(_heart.HitSlot, Is.EqualTo(StormHeartProcessor.Slot(_time.Time, 1, _config)));
            Assert.That(_heart.Strength, Is.EqualTo(2f / 3f).Within(1e-4f), "one of three hits");
            Assert.That(_cure.FirstHit(origin, Vector3.forward, 100f), Is.Null, "the slot's target is spent");
        }

        [Test]
        public void Beam_PastTheTarget_OrWhileTheRoundIsNotRunning_HitsNothing()
        {
            Vector3 target = LiveTarget();

            Assert.That(_cure.FirstHit(target + Vector3.right * 10f + Vector3.back * 40f, Vector3.forward, 100f), Is.Null);

            _zone.State = EncounterZoneState.Warning;
            Assert.That(_cure.FirstHit(target + Vector3.back * 40f, Vector3.forward, 100f), Is.Null);
        }

        [Test]
        public void LastHit_CuresTheStorm_AndEndsTheRound()
        {
            _heart.Strength = 1f / 3f;
            Vector3 target = LiveTarget();
            Vector3 origin = target + Vector3.back * 40f;

            float? distance = _cure.FirstHit(origin, Vector3.forward, 100f);
            _cure.Answer(origin, Vector3.forward, distance!.Value);

            Assert.That(_heart.Strength, Is.Zero);
            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Resting), "the zone rests once cured");
            Assert.That(_events.Published, Has.Some.InstanceOf<EncounterZoneChangedEvent>());
            Assert.That(_events.Published, Has.Some.InstanceOf<StormTargetHitEvent>());
        }

        private Vector3 LiveTarget()
        {
            var live = StormHeartProcessor.LiveTarget(_zone.Centre, _heart.Altitude, 1, _config.Seed, _time.Time, _heart.HitSlot, _config);
            Assert.That(live, Is.Not.Null, "a target is up at the test's time");
            return live!.Value.Position;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
