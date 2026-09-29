#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Targeting;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ITargetingService.TryAcquireSegment"/>: the per-tick sweep of a projectile's path against real colliders
    /// (plan <c>cannon-and-hazards.md</c>, S1 targeting).
    /// </summary>
    public class TargetingSegmentTests
    {
        private sealed class TestTargetable : MonoBehaviour, ITargetable
        {
            public Vector3 AimPoint => transform.position;
            public bool IsTargetable => true;
            public IAbilityControllerBase? Controller { get; set; }
        }

        private readonly List<Object> _objects = new();
        private TargetingUseCase _targeting = null!;
        private TargetingDefinition _sweep = null!;

        [SetUp]
        public void SetUp()
        {
            _targeting = new TargetingUseCase(new TargetableRegistry(), new TargetingProcessor());
            _sweep = Track(ScriptableObject.CreateInstance<TargetingDefinition>());
            _sweep.Shape = TargetShape.Ray;
            _sweep.Radius = 0.25f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects) Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [Test]
        public void TargetOnTheSegment_EndsIt()
        {
            var target = Target(new Vector3(0f, 0f, 5f));

            Assert.That(Sweep(Vector3.zero, new Vector3(0f, 0f, 10f), out var hit), Is.True);
            Assert.That(hit.Target, Is.SameAs(target));
            Assert.That(hit.Distance, Is.EqualTo(4.25f).Within(0.05f), "the sphere touches the box's near face");
        }

        [Test]
        public void TargetBeyondTheSegment_IsNotHitThisTick()
        {
            Target(new Vector3(0f, 0f, 5f));

            Assert.That(Sweep(Vector3.zero, new Vector3(0f, 0f, 3f), out _), Is.False);
        }

        [Test]
        public void SolidNonTarget_EndsItWithoutATarget()
        {
            Solid(new Vector3(0f, 0f, 3f));
            Target(new Vector3(0f, 0f, 6f));

            Assert.That(Sweep(Vector3.zero, new Vector3(0f, 0f, 10f), out var hit), Is.True);
            Assert.That(hit.Target, Is.Null, "the wall stops the shot before the target");
        }

        [Test]
        public void IgnoredColliders_ArePassed()
        {
            var ownShip = Solid(new Vector3(0f, 0f, 3f));
            var target = Target(new Vector3(0f, 0f, 6f));

            Assert.That(Sweep(Vector3.zero, new Vector3(0f, 0f, 10f), out var hit, c => c.gameObject == ownShip), Is.True);
            Assert.That(hit.Target, Is.SameAs(target));
        }

        [Test]
        public void FilteredOutTargets_ArePassed()
        {
            var blocked = Track(ScriptableObject.CreateInstance<GameplayTag>());
            _sweep.BlockedTags.Add(blocked);
            var friendlyController = new FakeAbilityController();
            friendlyController.AddTag(blocked);
            Target(new Vector3(0f, 0f, 3f)).Controller = friendlyController;
            var target = Target(new Vector3(0f, 0f, 6f));
            target.Controller = new FakeAbilityController();

            Assert.That(Sweep(Vector3.zero, new Vector3(0f, 0f, 10f), out var hit), Is.True);
            Assert.That(hit.Target, Is.SameAs(target));
        }

        [Test]
        public void TriggersWithoutATarget_ArePassed()
        {
            Solid(new Vector3(0f, 0f, 3f)).GetComponent<Collider>().isTrigger = true;

            Assert.That(Sweep(Vector3.zero, new Vector3(0f, 0f, 10f), out _), Is.False);
        }

        [Test]
        public void ZeroLengthSegment_HitsNothing()
        {
            Target(Vector3.zero);

            Assert.That(Sweep(Vector3.zero, Vector3.zero, out _), Is.False);
        }

        private bool Sweep(Vector3 from, Vector3 to, out SegmentHit hit, System.Func<Collider, bool>? ignore = null)
        {
            Physics.SyncTransforms();
            return _targeting.TryAcquireSegment(from, to, _sweep, ignore, out hit);
        }

        private TestTargetable Target(Vector3 position) => Solid(position).AddComponent<TestTargetable>();

        private GameObject Solid(Vector3 position)
        {
            var box = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            box.transform.position = position;
            return box;
        }

        private T Track<T>(T obj) where T : Object
        {
            _objects.Add(obj);
            return obj;
        }
    }
}
