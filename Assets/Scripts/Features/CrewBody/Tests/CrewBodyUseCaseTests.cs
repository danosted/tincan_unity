#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Humanoid;
using TinCan.Tests.Fakes;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Features.CrewBody.Tests
{
    /// <summary>Covers CrewBodyUseCase: every humanoid gets the body under its visual root, the capsule goes, once.</summary>
    public class CrewBodyUseCaseTests
    {
        private sealed class FakeHumanoidActor : MonoBehaviour, IHumanoidActor
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => false;
            public bool IsPlayerCharacter => true;
        }

        private readonly List<Object> _created = new();
        private FakeActorRegistry _actors = null!;
        private CrewBodyView _prefab = null!;
        private CrewBodyUseCase _useCase = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _prefab = Make("CrewBody").AddComponent<CrewBodyView>();
            _useCase = new CrewBodyUseCase(_actors, _prefab);
        }

        [TearDown]
        public void TearDown()
        {
            _useCase.Dispose();
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        /// <summary>A player as on NetworkPlayer.prefab: a 2 m CharacterController, a Visual child drawing a capsule.</summary>
        private (FakeHumanoidActor Actor, Transform Visual) Humanoid()
        {
            var root = Make("Player");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.center = Vector3.zero;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.gameObject.AddComponent<MeshFilter>();
            visual.gameObject.AddComponent<MeshRenderer>();

            var smoothing = root.AddComponent<HumanoidVisualSmoothingView>();
            var serialized = new SerializedObject(smoothing);
            serialized.FindProperty("_visualRoot").objectReferenceValue = visual;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return (root.AddComponent<FakeHumanoidActor>(), visual);
        }

        [Test]
        public void ARegisteredHumanoid_GetsTheBodyAtItsFeet_AndLosesTheCapsule()
        {
            _useCase.Initialize();
            var (actor, visual) = Humanoid();

            _actors.Register(actor);

            var body = visual.GetComponentInChildren<CrewBodyView>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.transform.parent, Is.EqualTo(visual));
            Assert.That(body.transform.localPosition.y, Is.EqualTo(-1f).Within(1e-4f), "feet at the bottom of the 2 m capsule");
            Assert.That(visual.GetComponent<MeshRenderer>().enabled, Is.False);
        }

        [Test]
        public void HumanoidsAlreadyThere_GetTheBodyOnInitialize_OnlyOnce()
        {
            var (actor, visual) = Humanoid();
            _actors.Register(actor);

            _useCase.Initialize();
            _useCase.Attach(actor);

            Assert.That(visual.GetComponentsInChildren<CrewBodyView>().Length, Is.EqualTo(1));
        }

        [Test]
        public void ABodyUnderTheVisualRoot_IsOneOfTheBodyRenderers()
        {
            _useCase.Initialize();
            var (actor, visual) = Humanoid();
            var part = new GameObject("Part");
            part.transform.SetParent(_prefab.transform, false);
            part.AddComponent<MeshRenderer>();

            _actors.Register(actor);

            var renderers = actor.GetComponent<IHumanoidVisualAnchor>().BodyRenderers;
            Assert.That(renderers.Count, Is.EqualTo(2), "the switched-off capsule and the body's part");
        }
    }
}
