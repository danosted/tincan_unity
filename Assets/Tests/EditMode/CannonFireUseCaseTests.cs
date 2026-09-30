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
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="CannonFireUseCase"/>: aiming, firing on the press, reload, the per-tick sweep, hits and expiry.</summary>
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
            public Transform? ShipRoot => null;
            public float Yaw { get; private set; }
            public float Elevation { get; private set; }
            public List<int> Fired { get; } = new();
            public List<int> Ended { get; } = new();

            public void ServerSetAim(float yaw, float elevation) => (Yaw, Elevation) = (yaw, elevation);
            public void ServerShotFired(int shotId, Vector3 origin, Vector3 velocity) => Fired.Add(shotId);
            public void ServerShotEnded(int shotId, Vector3 point) => Ended.Add(shotId);
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

        /// <summary>Answers a segment sweep with a hit once the ball passes <see cref="HitBeyondZ"/>.</summary>
        private sealed class FakeTargeting : ITargetingService
        {
            public float? HitBeyondZ { get; set; }
            public ITargetable? Target { get; set; }
            public int Sweeps { get; private set; }

            public bool TryAcquire(ITargeter targeter, TargetingDefinition definition, out TargetResult result)
            {
                result = default;
                return false;
            }

            public bool TryAcquireSegment(Vector3 from, Vector3 to, TargetingDefinition definition, Func<Collider, bool>? ignore, out SegmentHit hit)
            {
                Sweeps++;
                hit = default;
                if (HitBeyondZ is not { } z || to.z < z) return false;
                hit = new SegmentHit(Target, to, (to - from).magnitude);
                return true;
            }
        }

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
            _config.Sweep = Create<TargetingDefinition>("TD_CannonballSweep");
            _config.MuzzleSpeed = 30f;
            _config.Gravity = 0f;
            _config.MaxLifetime = 2f;

            _cannon = new FakeCannon();
            _actors.Register(_cannon);
            _body = new FakeHumanoidMovementView("Gunner");
            _gunner = new FakeHumanoidCharacterView(_body);
            _abilities.GrantAbility(_gunner, _config.FireAbility);

            _useCase = new CannonFireUseCase(new FakeNetworkService(), _actors, _time, new FakeEventPublisher(), _abilities,
                _targeting, _occupancy, _config, new CannonballProcessor(), new CannonAimProcessor());
        }

        [TearDown]
        public void TearDown()
        {
            _cannon.Destroy();
            _body.Destroy();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Unmanned_NeverFires()
        {
            Step(fire: true);

            Assert.That(_cannon.Fired, Is.Empty);
        }

        [Test]
        public void Manned_FiresOnceOnThePress_NotWhileHeld()
        {
            _occupancy.Occupant = _gunner;

            Step(fire: true);
            Step(fire: true);
            Step(fire: true);

            Assert.That(_cannon.Fired, Has.Count.EqualTo(1));
            Assert.That(_useCase.ShotsInFlight, Is.EqualTo(1));
        }

        [Test]
        public void Reload_BlocksTheNextShot_UntilTheCooldownEnds()
        {
            _occupancy.Occupant = _gunner;
            Step(fire: true);
            Step(fire: false);

            Step(fire: true);
            Assert.That(_cannon.Fired, Has.Count.EqualTo(1), "reloading");

            while (_time.Tick < 31) Step(fire: false);
            Step(fire: true);
            Assert.That(_cannon.Fired, Has.Count.EqualTo(2));
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
            _occupancy.Occupant = _gunner;
            _gunner.InputState = new HumanoidInputState { StationAim = new Vector2(170f, 89f) };

            _useCase.Tick();

            Assert.That(_cannon.Yaw, Is.EqualTo(_config.YawLimit).Within(1e-3f), "held at the limit, never wrapped");
            Assert.That(_cannon.Elevation, Is.EqualTo(_config.MaxElevation).Within(1e-3f));
        }

        [Test]
        public void Hit_AppliesTheHitEffect_AndEndsTheShot()
        {
            var hitTag = Create<GameplayTag>("State.Hit");
            var hitEffect = Create<GameplayEffectDefinition>("GE_Hit");
            hitEffect.DurationType = DurationType.Infinite;
            hitEffect.Modifiers = new List<AttributeModifier>();
            hitEffect.GrantedTags = new List<GameplayTag> { hitTag };
            _config.HitEffect = hitEffect;
            var target = new FakeAbilityController();
            _targeting.Target = new FakeTargetable { Controller = target };
            _targeting.HitBeyondZ = 3f; // 30 m/s straight ahead: past 3 m on the fourth tick of flight
            _occupancy.Occupant = _gunner;

            Step(fire: true);
            for (int i = 0; i < 10 && _cannon.Ended.Count == 0; i++) Step(fire: false);

            Assert.That(target.HasTag(hitTag), Is.True);
            Assert.That(_cannon.Ended, Is.EqualTo(_cannon.Fired));
            Assert.That(_useCase.ShotsInFlight, Is.Zero);
        }

        [Test]
        public void Miss_EndsTheShot_WhenItsLifetimeRunsOut()
        {
            _occupancy.Occupant = _gunner;

            Step(fire: true);
            while (_time.Tick < 61) Step(fire: false);

            Assert.That(_cannon.Ended, Has.Count.EqualTo(1));
            Assert.That(_useCase.ShotsInFlight, Is.Zero);
        }

        [Test]
        public void EachTick_SweepsOnlyThatTicksPieceOfTheArc()
        {
            _occupancy.Occupant = _gunner;
            Step(fire: true);
            int afterFire = _targeting.Sweeps;

            Step(fire: false);
            Step(fire: false);

            Assert.That(_targeting.Sweeps - afterFire, Is.EqualTo(2), "one sweep per tick per shot");
        }

        private void Step(bool fire)
        {
            _gunner.InputState = new HumanoidInputState { ActiveInputMask = fire ? 1UL << _fireInput.BitIndex : 0UL };
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

        private T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _assets.Add(asset);
            return asset;
        }
    }
}
