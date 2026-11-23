#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Features.Stations;
using TinCan.Core.Targeting;
using TinCan.Features.Weapons.Cannon;
using UnityEngine;
using TinCan.Tests.Fakes;
using Object = UnityEngine.Object;

namespace TinCan.Features.Weapons.Cannon.Tests
{
    /// <summary><see cref="CannonFireUseCase"/>: aiming, the charge on the press, the cooldown, the beam's one sweep, hits and misfires.</summary>
    public class CannonFireUseCaseTests
    {
        private sealed class FakeCannon : ICannon, IStation
        {
            private readonly GameObject _root = new("Cannon");
            private readonly GameObject _muzzle = new("Muzzle");

            public FakeCannon() => _muzzle.transform.SetParent(_root.transform, false);

            public Guid Id { get; } = EntityIds.New();
            public bool IsSimulating => true;
            public Transform? Base => _root.transform;
            public Transform? YawPivot => _root.transform;
            public Transform? PitchPivot => _root.transform;
            public Transform? Muzzle => _muzzle.transform;
            public Transform? ShipRoot { get; set; }
            public TinCan.Core.Domain.Abilities.IAbilityControllerBase? ShipController { get; set; }
            public float Yaw { get; private set; }
            public float Elevation { get; private set; }
            public List<int> Charging { get; } = new();
            public List<(int Id, Vector3 Origin, Vector3 End)> Fired { get; } = new();

            public void ServerSetAim(float yaw, float elevation) => (Yaw, Elevation) = (yaw, elevation);
            public void ServerShotCharging(int shotId) => Charging.Add(shotId);
            public void ServerShotFired(int shotId, Vector3 origin, Vector3 end) => Fired.Add((shotId, origin, end));
            public bool TryTakeShotEvent(out CannonShotEvent shotEvent) { shotEvent = default; return false; }
            public void ApplyAim(float yaw, float elevation) { }
            public void ShowAimPreview(Vector3[]? points, int count) { }

            public Transform? Seat => null;
            public AbilityDefinition? OccupyAbility => null;
            public IReadOnlyList<AbilityDefinition> GrantedAbilities => Array.Empty<AbilityDefinition>();
            public ulong? OccupantClientId => null;
            public Camera? ViewCamera => null;
            public void ServerSetOccupant(IHumanoidCharacterView? occupant) { }
            public InteractionDefinition Definition => null!;

            public void Destroy() => Object.DestroyImmediate(_root);
        }

        private sealed class FakeOccupancy : IStationOccupancy
        {
            public IHumanoidCharacterView? Occupant { get; set; }
            public bool TryOccupy(IHumanoidCharacterView player, IStation station) => false;
            public bool Leave(Guid playerId) => false;
            public IStation? StationOf(Guid playerId) => null;
            public IHumanoidCharacterView? OccupantOf(IStation station) => Occupant;
        }

        private sealed class FakeTargetable : ITargetable
        {
            public Vector3 AimPoint => Vector3.zero;
            public bool IsTargetable => true;
            public TinCan.Core.Domain.Abilities.IAbilityControllerBase? Controller { get; set; }
        }

        /// <summary>
        /// Answers a sweep with a hit on <see cref="Target"/> (and <see cref="Collider"/>) <see cref="HitAt"/> metres along
        /// it when the sweep is that long, records each sweep, and keeps the ignore rule it was asked with.
        /// </summary>
        private sealed class FakeTargeting : ITargetingService
        {
            public float? HitAt { get; set; }
            public ITargetable? Target { get; set; }
            public Collider? Collider { get; set; }
            /// <summary>Colliders the ignore rule is asked about during each sweep (it only knows the shot while sweeping).</summary>
            public List<Collider> Probe { get; } = new();
            public Dictionary<Collider, bool> Ignored { get; } = new();
            public List<(Vector3 From, Vector3 To)> Sweeps { get; } = new();

            public bool TryAcquire(ITargeter targeter, TargetingDefinition definition, out TargetResult result)
            {
                result = default;
                return false;
            }

            public bool TryAcquireSegment(Vector3 from, Vector3 to, TargetingDefinition definition, Func<Collider, bool>? ignore, out SegmentHit hit)
            {
                Sweeps.Add((from, to));
                foreach (var collider in Probe) Ignored[collider] = ignore != null && ignore(collider);
                hit = default;
                if (HitAt is not { } distance || (to - from).magnitude < distance) return false;
                hit = new SegmentHit(Target, from + (to - from).normalized * distance, distance, Collider);
                return true;
            }
        }

        // 0.4 s at 30 ticks a second.
        private const int ChargeTicks = 12;

        private readonly List<Object> _assets = new();
        private FakeTimeService _time = null!;
        private FakeActorRegistry _actors = null!;
        private AbilitySystemUseCase _abilities = null!;
        private FakeTargeting _targeting = null!;
        private FakeOccupancy _occupancy = null!;
        private CannonConfig _config = null!;
        private FakeCannon _cannon = null!;
        private FakeHumanoidMovementView _body = null!;
        private FakeHumanoidCharacterView _gunner = null!;
        private CannonFireUseCase _useCase = null!;
        private GameplayInput _fireInput = null!;
        private readonly List<ISpiritBeamTargets> _beamTargets = new();

        [SetUp]
        public void SetUp()
        {
            _time = new FakeTimeService { Tick = 0 };
            _actors = new FakeActorRegistry();
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, _time, new FakeEventPublisher());
            _targeting = new FakeTargeting();
            _occupancy = new FakeOccupancy();

            _fireInput = Create<TinCan.Core.Gas.Inputs.PrimaryInput>("Input_Primary");
            _fireInput.BitIndex = 1;
            _config = Create<CannonConfig>("CannonConfig");
            _config.FireInput = _fireInput;
            _config.FireAbility = FireAbility(reloadSeconds: 1f);
            _config.Sweep = Create<TargetingDefinition>("TD_SpiritBeamSweep");
            _config.ChargeSeconds = 0.4f;
            _config.BeamRange = 100f;

            _cannon = new FakeCannon();
            _actors.Register(_cannon);
            _body = new FakeHumanoidMovementView("Gunner");
            _gunner = new FakeHumanoidCharacterView(_body);
            _abilities.GrantAbility(_gunner, _config.FireAbility);

            _useCase = new CannonFireUseCase(new FakeNetworkService(), _actors, _time, new FakeEventPublisher(), _abilities,
                _targeting, _occupancy, _config, new CannonAimProcessor(), _beamTargets);
        }

        [TearDown]
        public void TearDown()
        {
            _beamTargets.Clear();
            _cannon.Destroy();
            _body.Destroy();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Unmanned_NeverFires()
        {
            for (int i = 0; i < 20; i++) Step(fire: true);

            Assert.That(_cannon.Charging, Is.Empty);
            Assert.That(_cannon.Fired, Is.Empty);
        }

        [Test]
        public void Press_Charges_ThenFiresOnce_AfterTheCharge()
        {
            _occupancy.Occupant = _gunner;

            Step(fire: true);
            Assert.That(_cannon.Charging, Has.Count.EqualTo(1), "the press starts the charge");
            Assert.That(_useCase.ShotsCharging, Is.EqualTo(1));

            for (int i = 1; i < ChargeTicks; i++) Step(fire: true);
            Assert.That(_cannon.Fired, Is.Empty, "still charging");

            Step(fire: true);
            Assert.That(_cannon.Fired, Has.Count.EqualTo(1));
            Assert.That(_cannon.Fired[0].Id, Is.EqualTo(_cannon.Charging[0]));
            Assert.That(_useCase.ShotsCharging, Is.Zero);
            Assert.That(_cannon.Charging, Has.Count.EqualTo(1), "holding the button fires once");
        }

        [Test]
        public void Cooldown_BlocksTheNextShot_UntilItEnds()
        {
            _occupancy.Occupant = _gunner;
            Step(fire: true);
            Step(fire: false);

            Step(fire: true);
            Assert.That(_cannon.Charging, Has.Count.EqualTo(1), "cooling down");

            while (_time.Tick < 31) Step(fire: false);
            Step(fire: true);
            Assert.That(_cannon.Charging, Has.Count.EqualTo(2));
        }

        [Test]
        public void Aim_FollowsTheGunnersStationAim_NotTheBodysLook()
        {
            _occupancy.Occupant = _gunner;
            _body.Transform.rotation = Quaternion.Euler(0f, -50f, 0f);
            _gunner.InputState = new HumanoidInputState
            {
                LookRotation = Quaternion.Euler(0f, -90f, 0f), LookPitch = 40f, // the body's look: ignored
                StationAim = new Vector2(30f, 10f),
            };

            _useCase.Tick();

            Assert.That(_cannon.Yaw, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(_cannon.Elevation, Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void Aim_OutsideTheLimits_IsClampedByTheServer()
        {
            _config.YawLimit = 105f;
            _occupancy.Occupant = _gunner;
            _gunner.InputState = new HumanoidInputState { StationAim = new Vector2(170f, 89f) };

            _useCase.Tick();

            Assert.That(_cannon.Yaw, Is.EqualTo(105f).Within(1e-3f), "a limited cannon is held at the limit, never wrapped");
            Assert.That(_cannon.Elevation, Is.EqualTo(_config.MaxElevation).Within(1e-3f));
        }

        [Test]
        public void Aim_FullTurn_IsWrappedByTheServer()
        {
            Assert.That(_config.AimLimits.TurnsFully, Is.True, "a cannon turns all the way round by default");
            _occupancy.Occupant = _gunner;
            _gunner.InputState = new HumanoidInputState { StationAim = new Vector2(200f, 10f) };

            _useCase.Tick();

            Assert.That(_cannon.Yaw, Is.EqualTo(-160f).Within(1e-3f));
        }

        [Test]
        public void Beam_IsOneSweep_FromTheMuzzle_AlongTheAimAtTheEndOfTheCharge()
        {
            _occupancy.Occupant = _gunner;
            Step(fire: true);
            // The gunner turns the barrel to 90 (right) while it charges: the beam goes where it points when it fires.
            for (int i = 1; i <= ChargeTicks; i++) Step(fire: false, aim: new Vector2(90f, 0f));

            Assert.That(_targeting.Sweeps, Has.Count.EqualTo(1));
            var (from, to) = _targeting.Sweeps[0];
            Assert.That(Vector3.Distance(from, _cannon.Muzzle!.position), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(to, from + Vector3.right * _config.BeamRange), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(_cannon.Fired[0].End, to), Is.LessThan(1e-3f), "a miss ends at its range");
        }

        [Test]
        public void Sweep_PassesTheFiringCannon_ButNotItsShip()
        {
            var ship = new GameObject("Ship");
            var hull = new GameObject("Hull", typeof(BoxCollider));
            hull.transform.SetParent(ship.transform, false);
            var carriage = new GameObject("Carriage", typeof(BoxCollider));
            carriage.transform.SetParent(_cannon.Base, false);
            _cannon.Base!.SetParent(ship.transform, false);
            _cannon.ShipRoot = ship.transform;
            _occupancy.Occupant = _gunner;
            var carriageCollider = carriage.GetComponent<Collider>();
            var hullCollider = hull.GetComponent<Collider>();
            _targeting.Probe.Add(carriageCollider);
            _targeting.Probe.Add(hullCollider);

            try
            {
                FireAndWait();

                Assert.That(_targeting.Ignored[carriageCollider], Is.True, "the beam starts inside its own cannon");
                Assert.That(_targeting.Ignored[hullCollider], Is.False, "its own ship is hit, not passed");
            }
            finally
            {
                _cannon.Base!.SetParent(null, false);
                Object.DestroyImmediate(ship);
            }
        }

        [Test]
        public void OwnShipHit_AppliesTheHitEffectToTheShip()
        {
            var hitTag = Create<GameplayTag>("State.Hit");
            _config.HitEffect = TagEffect("GE_Hit", hitTag);

            var ship = new GameObject("Ship");
            var hull = new GameObject("Hull", typeof(BoxCollider));
            hull.transform.SetParent(ship.transform, false);
            var shipController = new FakeAbilityController();
            _cannon.ShipRoot = ship.transform;
            _cannon.ShipController = shipController;
            // The hull is not a target: the sweep reports a hit on nothing, as on rock.
            _targeting.Target = null;
            _targeting.Collider = hull.GetComponent<Collider>();
            _targeting.HitAt = 3f;
            _occupancy.Occupant = _gunner;

            try
            {
                FireAndWait();

                Assert.That(shipController.HasTag(hitTag), Is.True, "the misfire hurts the ship");
                Assert.That(_cannon.Fired, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        [Test]
        public void Hit_AppliesTheHitEffect_AndTheBeamEndsThere()
        {
            var hitTag = Create<GameplayTag>("State.Hit");
            _config.HitEffect = TagEffect("GE_Hit", hitTag);
            var target = new FakeAbilityController();
            _targeting.Target = new FakeTargetable { Controller = target };
            _targeting.HitAt = 30f;
            _occupancy.Occupant = _gunner;

            FireAndWait();

            Assert.That(target.HasTag(hitTag), Is.True);
            var (_, origin, end) = _cannon.Fired[0];
            Assert.That((end - origin).magnitude, Is.EqualTo(30f).Within(1e-3f));
        }

        [Test]
        public void BeamTarget_NearerThanAnythingSolid_AnswersTheHit_AndEndsTheBeam()
        {
            var hitTag = Create<GameplayTag>("State.Hit");
            _config.HitEffect = TagEffect("GE_Hit", hitTag);
            var solid = new FakeAbilityController();
            _targeting.Target = new FakeTargetable { Controller = solid };
            _targeting.HitAt = 50f;
            var targets = new FakeBeamTargets { At = 20f };
            _beamTargets.Add(targets);
            _occupancy.Occupant = _gunner;

            FireAndWait();

            Assert.That(targets.Answered, Is.EqualTo(new[] { 20f }));
            Assert.That(targets.AskedLength, Is.EqualTo(50f).Within(1e-3f), "asked as far as the first solid thing");
            Assert.That(solid.HasTag(hitTag), Is.False, "the beam ended before the solid target");
            var (_, origin, end) = _cannon.Fired[0];
            Assert.That((end - origin).magnitude, Is.EqualTo(20f).Within(1e-3f));
        }

        [Test]
        public void BeamTarget_BehindSomethingSolid_IsNotHit()
        {
            var hitTag = Create<GameplayTag>("State.Hit");
            _config.HitEffect = TagEffect("GE_Hit", hitTag);
            var solid = new FakeAbilityController();
            _targeting.Target = new FakeTargetable { Controller = solid };
            _targeting.HitAt = 10f;
            var targets = new FakeBeamTargets { At = 20f };
            _beamTargets.Add(targets);
            _occupancy.Occupant = _gunner;

            FireAndWait();

            Assert.That(targets.Answered, Is.Empty);
            Assert.That(solid.HasTag(hitTag), Is.True);
        }

        /// <summary>A beam target <see cref="At"/> metres along any beam that reaches it.</summary>
        private sealed class FakeBeamTargets : ISpiritBeamTargets
        {
            public float At { get; set; }
            public float AskedLength { get; private set; }
            public List<float> Answered { get; } = new();

            public float? FirstHit(Vector3 origin, Vector3 direction, float length)
            {
                AskedLength = length;
                return At <= length ? At : null;
            }

            public void Answer(Vector3 origin, Vector3 direction, float distance) => Answered.Add(distance);
        }

        private void FireAndWait()
        {
            Step(fire: true);
            for (int i = 0; i < ChargeTicks; i++) Step(fire: false);
        }

        private void Step(bool fire, Vector2 aim = default)
        {
            _gunner.InputState = new HumanoidInputState { ActiveInputMask = fire ? 1UL << _fireInput.BitIndex : 0UL, StationAim = aim };
            _useCase.Tick();
            _time.Tick++;
        }

        private AbilityDefinition FireAbility(float reloadSeconds)
        {
            var ability = Create<AbilityDefinition>("GA_FireCannon");
            ability.EndsImmediately = true;
            ability.CancelAbilitiesWithTag = new List<GameplayTag>();
            ability.BlockAbilitiesWithTag = new List<GameplayTag>();
            ability.ActivationRequiredTagsOnActor = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnActor = new List<GameplayTag>();
            ability.ActivationRequiredTagsOnTarget = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnTarget = new List<GameplayTag>();
            ability.TimingTagWindows = new List<AbilityTagWindow>();
            var reload = Create<GameplayEffectDefinition>("GE_CannonReload");
            reload.DurationType = DurationType.Duration;
            reload.DurationSeconds = reloadSeconds;
            reload.Modifiers = new List<AttributeModifier>();
            reload.GrantedTags = new List<GameplayTag>();
            ability.CooldownEffect = reload;
            return ability;
        }

        private GameplayEffectDefinition TagEffect(string name, GameplayTag tag)
        {
            var effect = Create<GameplayEffectDefinition>(name);
            effect.DurationType = DurationType.Infinite;
            effect.Modifiers = new List<AttributeModifier>();
            effect.GrantedTags = new List<GameplayTag> { tag };
            return effect;
        }

        private T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _assets.Add(asset);
            return asset;
        }
    }
}
