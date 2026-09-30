#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Domain.Input;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Features.Stations;
using TinCan.Features.Weapons.Cannon;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="GunnerAimUseCase"/>: while the Gunner context is live the mouse steers the barrel within its limits,
    /// starting from where it points, and the aim travels in the predicted input instead of the body's look.
    /// </summary>
    public class GunnerAimUseCaseTests
    {
        private sealed class MannedCannon : ICannon, IStation
        {
            public Guid Id { get; } = EntityIds.New();
            public bool IsSimulating => true;
            public Transform? Base => null;
            public Transform? YawPivot => null;
            public Transform? PitchPivot => null;
            public Transform? Muzzle => null;
            public Transform? ShipRoot => null;
            public float Yaw { get; set; }
            public float Elevation { get; set; }
            public ulong? OccupantClientId { get; set; }

            public void ServerSetAim(float yaw, float elevation) { }
            public void ServerShotFired(int shotId, Vector3 origin, Vector3 velocity) { }
            public void ServerShotEnded(int shotId, Vector3 point) { }
            public bool TryTakeShotEvent(out CannonShotEvent shotEvent) { shotEvent = default; return false; }
            public void ApplyAim(float yaw, float elevation) { }
            public void ShowAimPreview(Vector3[]? points, int count) { }
            public Transform? Seat => null;
            public AbilityDefinition? OccupyAbility => null;
            public IReadOnlyList<AbilityDefinition> GrantedAbilities => Array.Empty<AbilityDefinition>();
            public Camera? ViewCamera => null;
            public void ServerSetOccupant(IHumanoidCharacterView? occupant) { }
            public InteractionDefinition Definition => null!;
        }

        private sealed class SwitchableContexts : IInputContexts
        {
            public readonly List<InputContext> Live = new();
            public IReadOnlyList<InputContext> Active => Live;
            public bool IsActive(InputContext? context) => context != null && Live.Contains(context);
            public event Action? Changed { add { } remove { } }
        }

        private GunnerInputContext _controls = null!;
        private CannonConfig _config = null!;
        private FakeInputReader _input = null!;
        private SwitchableContexts _contexts = null!;
        private FakeActorRegistry _actors = null!;
        private MannedCannon _cannon = null!;
        private GunnerAimUseCase _useCase = null!;

        [SetUp]
        public void SetUp()
        {
            _controls = ScriptableObject.CreateInstance<GunnerInputContext>();
            _controls.Aim = InputActionId.Create("Gunner/Aim");
            _config = ScriptableObject.CreateInstance<CannonConfig>();
            _config.YawLimit = 60f;
            _config.MinElevation = -10f;
            _config.MaxElevation = 40f;
            _config.AimSensitivity = 0.5f;
            _input = new FakeInputReader();
            _contexts = new SwitchableContexts();
            _actors = new FakeActorRegistry();
            _cannon = new MannedCannon { Yaw = 12f, Elevation = 3f, OccupantClientId = 0 };
            _actors.Register(_cannon);
            _useCase = new GunnerAimUseCase(_input, _controls, _contexts, _actors, new FakeNetworkService { LocalClientId = 0 }, _config, new CannonAimProcessor());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_controls);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void GunnerContextOff_NoAim_AndTheInputKeepsZero()
        {
            _useCase.Tick();

            var input = new HumanoidInputState();
            _useCase.Contribute(null!, ref input);

            Assert.That(_useCase.LocalAim, Is.Null);
            Assert.That(input.StationAim, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Manning_StartsWhereTheBarrelPoints_ThenTheMouseSteers()
        {
            _contexts.Live.Add(_controls);

            _useCase.Tick();
            Assert.That(_useCase.LocalAim!.Value.Aim, Is.EqualTo(new Vector2(12f, 3f)));

            _input.Vectors[_controls.Aim!] = new Vector2(10f, 4f);
            _useCase.Tick();

            var input = new HumanoidInputState();
            _useCase.Contribute(null!, ref input);
            Assert.That(input.StationAim.x, Is.EqualTo(17f).Within(1e-4f));
            Assert.That(input.StationAim.y, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void LongSweep_StopsAtTheYawLimit()
        {
            _contexts.Live.Add(_controls);
            _input.Vectors[_controls.Aim!] = new Vector2(40f, 0f);

            for (int frame = 0; frame < 60; frame++) _useCase.Tick();

            Assert.That(_useCase.LocalAim!.Value.Aim.x, Is.EqualTo(60f).Within(1e-4f));
        }

        [Test]
        public void SomeoneElsesCannon_IsNotAimed()
        {
            _cannon.OccupantClientId = 7;
            _contexts.Live.Add(_controls);

            _useCase.Tick();

            Assert.That(_useCase.LocalAim, Is.Null);
            Assert.That(_useCase.TrySetAim(Vector2.zero), Is.False);
        }

        [Test]
        public void TrySetAim_IsClampedToo()
        {
            _contexts.Live.Add(_controls);
            _useCase.Tick();

            Assert.That(_useCase.TrySetAim(new Vector2(0f, 90f)), Is.True);
            Assert.That(_useCase.LocalAim!.Value.Aim, Is.EqualTo(new Vector2(0f, 40f)));
        }
    }
}
