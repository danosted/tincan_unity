#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Targeting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="TargetShape.Look"/> against real colliders: what the look ray hits wins whatever its pivot; otherwise the
    /// best-aligned target in a narrow cone, measured to its colliders, with line of sight (plan interaction-targeting.md).
    /// </summary>
    public class TargetingLookTests
    {
        private sealed class TestTargetable : MonoBehaviour, ITargetable
        {
            public Vector3 AimPoint => transform.position;
            public bool IsTargetable => true;
            public IAbilityControllerBase? Controller => null;
        }

        private sealed class EyeTargeter : ITargeter
        {
            public Guid Id { get; } = Guid.NewGuid();

            public bool TryGetOrigin(out TargetingOrigin origin)
            {
                origin = new TargetingOrigin(Vector3.zero, Quaternion.identity, 1.5f, 0f);
                return true;
            }
        }

        private readonly List<Object> _objects = new();
        private TargetableRegistry _registry = null!;
        private TargetingUseCase _targeting = null!;
        private TargetingDefinition _look = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new TargetableRegistry();
            _targeting = new TargetingUseCase(_registry, new TargetingProcessor());
            _look = Track(ScriptableObject.CreateInstance<TargetingDefinition>());
            _look.Source = AimSource.EyeAim;
            _look.Shape = TargetShape.Look;
            _look.Range = 3f;
            _look.HorizontalAngle = 30f;
            _look.VerticalAngle = 30f;
            _look.Selection = TargetSelection.BestAligned;
            _look.RequireLineOfSight = true;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects) Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [Test]
        public void WhatYouLookAt_BeatsANearerTargetBesideIt()
        {
            var lookedAt = Target(new Vector3(0f, 1.5f, 2.5f), Vector3.one * 0.5f);
            Target(new Vector3(0.45f, 1.5f, 1.2f), Vector3.one * 0.2f);

            Assert.That(Acquire(out var result), Is.True);
            Assert.That(result.Target, Is.SameAs(lookedAt));
        }

        [Test]
        public void ATallObject_IsHitWhereYouLook_ThoughItsPivotIsOutOfReach()
        {
            // The pivot is on the "deck" far below the eye; the box reaches up into the look.
            var pivot = Pivot(new Vector3(0f, -3f, 2f));
            Part(pivot, new Vector3(0f, 4.5f, 0f), new Vector3(1f, 1f, 1f));

            Assert.That(Acquire(out var result), Is.True);
            Assert.That(result.Target, Is.SameAs(pivot));
            Assert.That(result.Distance, Is.EqualTo(1.5f).Within(0.05f), "measured to where the ray hit the box");
        }

        [Test]
        public void JustOffTheLook_TheConeFindsIt_ByItsClosestPointNotItsPivot()
        {
            // The ray passes beside the box; its pivot is far to the side (56 deg off the look), its near edge 3 deg off.
            var pivot = Pivot(new Vector3(3f, 1.5f, 2f));
            Part(pivot, new Vector3(-2.3f, 0f, 0f), new Vector3(1f, 1f, 1f));
            _registry.Register(pivot);

            Assert.That(Acquire(out var result), Is.True);
            Assert.That(result.Target, Is.SameAs(pivot));
        }

        [Test]
        public void ATargetBehindAWall_IsNotAcquired()
        {
            Solid(new Vector3(0f, 1.5f, 1.2f), new Vector3(3f, 3f, 0.2f));
            _registry.Register(Target(new Vector3(0.3f, 1.5f, 2.5f), Vector3.one * 0.5f));

            Assert.That(Acquire(out _), Is.False);
        }

        [Test]
        public void TheFilter_LimitsWhatCountsAsATarget()
        {
            Target(new Vector3(0f, 1.5f, 2f), Vector3.one * 0.5f);

            Assert.That(_targeting.TryAcquire(new EyeTargeter(), _look, _ => false, out _), Is.False);
        }

        [Test]
        public void TheTargetersOwnBody_DoesNotBlockTheLook()
        {
            _look.Radius = 0.05f; // a sphere cast starting inside the body reports it at distance 0
            Solid(new Vector3(0f, 1f, 0f), new Vector3(0.6f, 2f, 0.6f));
            var target = Target(new Vector3(0f, 1.5f, 2f), Vector3.one * 0.5f);

            Assert.That(Acquire(out var result), Is.True);
            Assert.That(result.Target, Is.SameAs(target));
        }

        [Test]
        public void OutOfReach_IsNotAcquired()
        {
            _registry.Register(Target(new Vector3(0f, 1.5f, 4f), Vector3.one * 0.5f));

            Assert.That(Acquire(out _), Is.False);
        }

        private bool Acquire(out TargetResult result)
        {
            Physics.SyncTransforms();
            return _targeting.TryAcquire(new EyeTargeter(), _look, out result);
        }

        private TestTargetable Target(Vector3 position, Vector3 size) => Solid(position, size).AddComponent<TestTargetable>();

        private TestTargetable Pivot(Vector3 position)
        {
            var pivot = Track(new GameObject("Pivot"));
            pivot.transform.position = position;
            return pivot.AddComponent<TestTargetable>();
        }

        private void Part(Component pivot, Vector3 localPosition, Vector3 size)
        {
            var part = Solid(Vector3.zero, size);
            part.transform.SetParent(pivot.transform, false);
            part.transform.localPosition = localPosition;
        }

        private GameObject Solid(Vector3 position, Vector3 size)
        {
            var box = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            box.transform.position = position;
            box.transform.localScale = size;
            return box;
        }

        private T Track<T>(T obj) where T : Object
        {
            _objects.Add(obj);
            return obj;
        }
    }
}
