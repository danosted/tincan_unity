#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Features.Encounters;
using TinCan.Features.SkyHazards;
using TinCan.Tests.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace TinCan.Features.Storm.Tests
{
    /// <summary>
    /// The storm fights back (storm-crossing.md, ST3 and ST4): its swirling wind, the gust waves (the same push from the
    /// same seed and time), <see cref="StormGustUseCase"/> shoving a ship inside it, and <see cref="StormDebrisUseCase"/>
    /// throwing debris at it from upwind.
    /// </summary>
    public class StormWindTests
    {
        private sealed class Zone : IEncounterZone
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public string DisplayName => "Storm";
            public Vector2 Centre { get; set; } = Vector2.zero;
            public float Radius => 750f;
            public EncounterZoneState State { get; set; } = EncounterZoneState.Idle;
            public double StateEndsAt => 0d;
            public void ServerSetCentre(Vector2 centre) => Centre = centre;
            public void ServerSetState(EncounterZoneState state, double endsAt) => State = state;
        }

        private sealed class Hazards : ISkyHazards
        {
            public readonly List<(Vector3 Start, Vector3 Velocity, float Lifetime)> Launches = new();
            public IReadOnlyList<ISkyHazard> Alive => Array.Empty<ISkyHazard>();
            public int Destroyed => 0;
            public int Hits => 0;
            public ISkyHazard? SpawnAt(Vector3 position) => null;

            public ISkyHazard? Launch(Vector3 position, Vector3 velocity, float lifetime)
            {
                Launches.Add((position, velocity, lifetime));
                return new Piece();
            }

            private sealed class Piece : ISkyHazard
            {
                public Transform? Transform => null;
                public TinCan.Core.Domain.Abilities.IAbilityControllerBase? Controller => null;
            }
        }

        private readonly List<Object> _assets = new();
        private StormConfig _config = null!;
        private EncounterZoneConfig _zoneConfig = null!;
        private FakeTimeService _time = null!;
        private FakeActorRegistry _actors = null!;
        private Zone _zone = null!;
        private FakeAirshipView _ship = null!;

        // 0.4 of the radius out from the centre: depth 0.6, in the bands (full profile), so the fury is 0.6.
        private static readonly Vector3 InTheBands = new(300f, 40f, 0f);

        [SetUp]
        public void SetUp()
        {
            _config = Create<StormConfig>();
            _zoneConfig = Create<EncounterZoneConfig>();
            _time = new FakeTimeService { Time = 0f, DeltaTime = 0.5f };
            _actors = new FakeActorRegistry();
            _zone = new Zone();
            _actors.Register(_zone);
            _ship = new FakeAirshipView();
            _ship.Transform.position = InTheBands;
            _actors.Register(_ship);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Wind_SwirlsRoundTheCentre_Level()
        {
            var centre = new Vector2(10f, 20f);
            var point = new Vector3(110f, 55f, 20f);

            Vector3 wind = StormWindProcessor.Wind(centre, point);

            Assert.That(wind.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(wind.y, Is.Zero);
            Assert.That(Vector3.Dot(wind, Vector3.right), Is.EqualTo(0f).Within(1e-4f), "across the line from the centre, never out of the disc");
        }

        [Test]
        public void Fury_IsNothingOutsideOrCured_AndMostAtTheEyesEdge()
        {
            Assert.That(StormWindProcessor.Fury(0f, 1f), Is.Zero);
            Assert.That(StormWindProcessor.Fury(0.6f, 0f), Is.Zero);
            Assert.That(StormWindProcessor.Fury(0.8f, 1f), Is.GreaterThan(StormWindProcessor.Fury(0.4f, 1f)));
            Assert.That(StormWindProcessor.Fury(1f, 1f), Is.LessThan(0.01f), "the eye is still");
        }

        [Test]
        public void Gusts_ComeInWaves_TheSameFromTheSameSeedAndTime()
        {
            int gusting = 0, calm = 0;
            for (double t = 0d; t < 400d; t += 0.25d)
            {
                Vector3 a = StormWindProcessor.Gust(Vector2.zero, InTheBands, 0.8f, 1, 3, t, _config);
                Vector3 b = StormWindProcessor.Gust(Vector2.zero, InTheBands, 0.8f, 1, 3, t, _config);
                Assert.That(a, Is.EqualTo(b));
                if (a.sqrMagnitude > 0.01f) gusting++;
                else calm++;
            }

            Assert.That(gusting, Is.GreaterThan(0), "some gusts");
            Assert.That(calm, Is.GreaterThan(gusting), "calm between them");
            Assert.That(StormWindProcessor.Gust(Vector2.zero, InTheBands, 0f, 1, 3, 1d, _config), Is.EqualTo(Vector3.zero), "no fury, no gust");
        }

        [Test]
        public void Gusts_AreHarderInHarderStorms()
        {
            double t = FirstGustPeak(0.8f, 3);

            float mild = StormWindProcessor.Gust(Vector2.zero, InTheBands, 0.8f, 1, 3, t, _config).magnitude;
            float wild = StormWindProcessor.Gust(Vector2.zero, InTheBands, 0.8f, 3, 3, t, _config).magnitude;

            Assert.That(mild, Is.GreaterThan(0f));
            Assert.That(wild, Is.EqualTo(mild * 2f).Within(1e-3f));
        }

        [Test]
        public void GustUseCase_ShovesAShipInTheStorm_NotOneOutsideIt_NorOnceItIsCured()
        {
            var gusts = new StormGustUseCase(new FakeNetworkService(), _actors, _time, _config, _zoneConfig, _ => null);
            _time.Time = (float)FirstGustPeak(0.6f, _config.Seed);

            Assert.That(gusts.Evaluate(_ship, 0f, 0f, 0.5f).Horizontal.magnitude, Is.GreaterThan(0.5f), "in the bands");

            _zone.State = EncounterZoneState.Resting;
            Assert.That(gusts.Evaluate(_ship, 0f, 0f, 0.5f).Horizontal, Is.EqualTo(Vector3.zero), "cured: calm");

            _zone.State = EncounterZoneState.Idle;
            _ship.Transform.position = new Vector3(2000f, 40f, 0f);
            Assert.That(gusts.Evaluate(_ship, 0f, 0f, 0.5f).Horizontal, Is.EqualTo(Vector3.zero), "outside the storm");
        }

        [Test]
        public void Debris_ComesFromUpwind_OnTheWind_AtItsRate()
        {
            var hazards = new Hazards();
            var debris = new StormDebrisUseCase(new FakeNetworkService(), _actors, _time, hazards, _config, _zoneConfig, _ => null, new Random(1));

            for (int i = 0; i < 120; i++) debris.Tick(); // a minute

            float expected = StormWindProcessor.DebrisRate(0.6f, 1, _config) * 60f;
            Assert.That(hazards.Launches.Count, Is.EqualTo(Mathf.FloorToInt(expected)).Within(1));
            Vector3 wind = StormWindProcessor.Wind(Vector2.zero, InTheBands);
            foreach (var (start, velocity, lifetime) in hazards.Launches)
            {
                Assert.That(Vector3.Dot(start - InTheBands, wind), Is.LessThan(-_config.DebrisDistance * 0.9f), "upwind of the ship");
                Assert.That(Vector3.Dot(velocity.normalized, wind), Is.GreaterThan(0.999f), "carried on the wind");
                Assert.That(lifetime * _config.DebrisSpeed, Is.GreaterThan(_config.DebrisDistance), "it blows past before it goes");
            }
        }

        [Test]
        public void Debris_NoneOutsideTheStorm()
        {
            var hazards = new Hazards();
            var debris = new StormDebrisUseCase(new FakeNetworkService(), _actors, _time, hazards, _config, _zoneConfig, _ => null, new Random(1));
            _ship.Transform.position = new Vector3(2000f, 40f, 0f);

            for (int i = 0; i < 120; i++) debris.Tick();

            Assert.That(hazards.Launches, Is.Empty);
        }

        [Test]
        public void Debris_IsAimedAtWhereTheShipWillBe()
        {
            var shipVelocity = new Vector3(0f, 0f, 8f);
            var (start, velocity, _) = StormWindProcessor.Debris(Vector2.zero, InTheBands, shipVelocity, Vector2.zero, _config);

            float flight = _config.DebrisDistance / _config.DebrisSpeed;
            Assert.That(Vector3.Distance(start + velocity * flight, InTheBands + shipVelocity * flight), Is.LessThan(1e-3f));
        }

        // The middle of the first gust at this fury and seed.
        private double FirstGustPeak(float fury, int seed)
        {
            for (double t = 0d; t < 400d; t += 0.25d)
            {
                if (StormWindProcessor.GustShare(fury, seed, t, _config) > 0.99f) return t;
            }
            Assert.Fail("no gust in 400 s");
            return 0d;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
