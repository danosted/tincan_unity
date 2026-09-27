#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Targeting;
using TinCan.Features.Abilities;
using TinCan.Features.Interaction;
using TinCan.Features.Targeting;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class TargetingUseCaseTests
    {
        private sealed class FakeTargetable : ITargetable
        {
            public Vector3 AimPoint { get; set; }
            public bool IsTargetable { get; set; } = true;
            public IAbilityControllerBase? Controller { get; set; }
        }

        private sealed class FakeTargeter : ITargeter
        {
            public Guid Id { get; } = Guid.NewGuid();
            public TargetingOrigin? Origin { get; set; } = new TargetingOrigin(Vector3.zero, Quaternion.identity, 1.5f);

            public bool TryGetOrigin(out TargetingOrigin origin)
            {
                origin = Origin ?? default;
                return Origin.HasValue;
            }
        }

        private readonly List<Object> _assets = new();
        private TargetableRegistry _registry = null!;
        private TargetingUseCase _targeting = null!;
        private TargetingDefinition _cone = null!;
        private GameplayTag _damaged = null!;
        private GameplayTag _beingRepaired = null!;
        private FakeTargeter _targeter = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new TargetableRegistry();
            _targeting = new TargetingUseCase(_registry, new TargetingProcessor());
            _damaged = Create<GameplayTag>("State.Damaged");
            _beingRepaired = Create<GameplayTag>("State.BeingRepaired");
            _cone = Create<TargetingDefinition>("TD_Test");
            _cone.Shape = TargetShape.Cone;
            _cone.Range = 2.5f;
            _cone.HorizontalAngle = 100f;
            _cone.VerticalAngle = 120f;
            _targeter = new FakeTargeter();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private FakeTargetable Add(Vector3 point, params GameplayTag[] tags)
        {
            var controller = new FakeAbilityController();
            foreach (var tag in tags) controller.AddTag(tag);
            var targetable = new FakeTargetable { AimPoint = point, Controller = controller };
            _registry.Register(targetable);
            return targetable;
        }

        [Test]
        public void Acquires_TheNearestRegisteredTargetInTheShape()
        {
            Add(new Vector3(0f, 1.5f, 2.2f));
            var near = Add(new Vector3(0f, 1.5f, 1.2f));

            Assert.That(_targeting.TryAcquire(_targeter, _cone, out var result), Is.True);
            Assert.That(result.Target, Is.SameAs(near));
            Assert.That(result.Distance, Is.EqualTo(1.2f).Within(0.001f));
        }

        [Test]
        public void RequiredTags_FilterOnTheTargetsController()
        {
            _cone.RequiredTags = new List<GameplayTag> { _damaged };
            Add(new Vector3(0f, 1.5f, 1f));
            var broken = Add(new Vector3(0f, 1.5f, 2f), _damaged);

            Assert.That(_targeting.TryAcquire(_targeter, _cone, out var result), Is.True);
            Assert.That(result.Target, Is.SameAs(broken), "The nearer, untagged target is filtered out.");
        }

        [Test]
        public void BlockedTags_ExcludeTargets()
        {
            _cone.BlockedTags = new List<GameplayTag> { _beingRepaired };
            Add(new Vector3(0f, 1.5f, 1f), _damaged, _beingRepaired);

            Assert.That(_targeting.TryAcquire(_targeter, _cone, out _), Is.False);
        }

        [Test]
        public void RequiredTags_WithoutAController_NeverMatch()
        {
            _cone.RequiredTags = new List<GameplayTag> { _damaged };
            _registry.Register(new FakeTargetable { AimPoint = new Vector3(0f, 1.5f, 1f) });

            Assert.That(_targeting.TryAcquire(_targeter, _cone, out _), Is.False);
        }

        [Test]
        public void NonTargetableAndUnregistered_AreIgnored()
        {
            var hidden = Add(new Vector3(0f, 1.5f, 1f));
            hidden.IsTargetable = false;
            var gone = Add(new Vector3(0f, 1.5f, 1.5f));
            _registry.Unregister(gone);

            Assert.That(_targeting.TryAcquire(_targeter, _cone, out _), Is.False);
        }

        [Test]
        public void TargeterWithoutAnOrigin_AcquiresNothing()
        {
            Add(new Vector3(0f, 1.5f, 1f));
            _targeter.Origin = null;

            Assert.That(_targeting.TryAcquire(_targeter, _cone, out _), Is.False);
        }

        private T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _assets.Add(asset);
            return asset;
        }
    }

    public class TargetableRegistrationTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void ActorOrchestrator_RegistersAnEntitysTargetablesOnce_AndNotThoseOfAChildEntity()
        {
            var registry = new TargetableRegistry();
            // Infrastructure lives in Assembly-CSharp; same reflection boundary as FuelFixtureRegistrationTests.
            var type = Type.GetType("TinCan.Core.Infrastructure.ActorOrchestrator, Assembly-CSharp", true)!;
            var orchestrator = (IActorOrchestrator)Activator.CreateInstance(type, new FakeActorRegistry(), new InteractorRegistry(), new FakeAbilityRegistry(), registry)!;

            var health = ScriptableObject.CreateInstance<HealthAttribute>();
            var maxHealth = ScriptableObject.CreateInstance<MaxHealthAttribute>();
            var ship = new GameObject("Ship");
            _created.Add(health);
            _created.Add(maxHealth);
            _created.Add(ship);
            var points = FakeShipDamage.AttachPoints(ship, 2, health, maxHealth);

            var shipEntity = ship.AddComponent<FakeEntity>();
            var fixture = points[1].gameObject.AddComponent<FakeEntity>(); // a fixture parented under the ship

            orchestrator.RegisterEntity(shipEntity);
            orchestrator.RegisterEntity(shipEntity);
            Assert.That(registry.All, Is.EqualTo(new[] { points[0] }), "Once, and without the child entity's targetable.");

            orchestrator.RegisterEntity(fixture);
            Assert.That(registry.All, Is.EquivalentTo(points));

            orchestrator.UnregisterEntity(shipEntity);
            Assert.That(registry.All, Is.EqualTo(new[] { points[1] }), "The fixture stays registered while it lives.");
        }
    }
}
