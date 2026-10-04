#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Targeting;
using TinCan.Features.TargetOutline;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="TargetOutlinePresenter"/>: the target's own meshes join the outline layer while it is the target, and
    /// leave it when the target changes; nested targets, lines and targets without a mesh are not outlined.
    /// </summary>
    public class TargetOutlinePresenterTests
    {
        private sealed class TestTargetable : MonoBehaviour, ITargetable
        {
            public Vector3 AimPoint => transform.position;
            public bool IsTargetable => true;
            public IAbilityControllerBase? Controller => null;
        }

        private sealed class NoContexts : IInputContexts
        {
            public IReadOnlyList<InputContext> Active => Array.Empty<InputContext>();
            public bool IsActive(InputContext? context) => false;
            public event Action? Changed { add { } remove { } }
        }

        private readonly List<Object> _objects = new();
        private TargetOutlineConfig _config = null!;
        private TargetOutlinePresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _config = Track(ScriptableObject.CreateInstance<TargetOutlineConfig>());
            _config.RenderingLayer = 30;
            _presenter = new TargetOutlinePresenter(new FakeActorRegistry(), new NoContexts(), Track(FakeInputContexts.Humanoid()), _config);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects) Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [Test]
        public void Target_ItsOwnMeshesAreOutlined_AndLeaveTheLayerWhenItChanges()
        {
            var (first, firstMesh) = TargetWithMesh("First");
            var (second, secondMesh) = TargetWithMesh("Second");

            _presenter.Show(first);
            Assert.That(Outlined(firstMesh), Is.True);
            Assert.That(_config.Highlighted, Is.EqualTo(1));

            _presenter.Show(second);
            Assert.That(Outlined(firstMesh), Is.False);
            Assert.That(Outlined(secondMesh), Is.True);

            _presenter.Show(null);
            Assert.That(Outlined(secondMesh), Is.False);
            Assert.That(_config.Highlighted, Is.Zero);
        }

        [Test]
        public void NestedTargetsAndLines_AreNotPartOfTheOutline()
        {
            var (ship, hull) = TargetWithMesh("Ship");
            var (fixture, fixtureMesh) = TargetWithMesh("Fixture");
            fixture.transform.SetParent(ship.transform, false);
            var line = Track(new GameObject("AimPreview")).AddComponent<LineRenderer>();
            line.transform.SetParent(ship.transform, false);

            _presenter.Show(ship);

            Assert.That(Outlined(hull), Is.True);
            Assert.That(Outlined(fixtureMesh), Is.False, "the fixture is its own target");
            Assert.That((line.renderingLayerMask & _config.LayerBit) != 0, Is.False);
        }

        [Test]
        public void ATargetWithoutAMesh_ShowsNothing()
        {
            var bare = Track(new GameObject("Bare")).AddComponent<TestTargetable>();

            _presenter.Show(bare);

            Assert.That(_config.Highlighted, Is.Zero);
        }

        [Test]
        public void TheOutlineLayer_IsAddedToTheRenderersOwnLayers_NotInPlaceOfThem()
        {
            var (target, mesh) = TargetWithMesh("Target");
            mesh.renderingLayerMask = 1u;

            _presenter.Show(target);
            Assert.That(mesh.renderingLayerMask, Is.EqualTo(1u | _config.LayerBit));

            _presenter.Show(null);
            Assert.That(mesh.renderingLayerMask, Is.EqualTo(1u));
        }

        private (TestTargetable Target, MeshRenderer Mesh) TargetWithMesh(string name)
        {
            var root = Track(new GameObject(name));
            var target = root.AddComponent<TestTargetable>();
            var mesh = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            mesh.transform.SetParent(root.transform, false);
            return (target, mesh.GetComponent<MeshRenderer>());
        }

        private bool Outlined(Renderer renderer) => (renderer.renderingLayerMask & _config.LayerBit) != 0;

        private T Track<T>(T obj) where T : Object
        {
            _objects.Add(obj);
            return obj;
        }
    }
}
