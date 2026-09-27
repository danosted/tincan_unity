#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Entities;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Entities;
using TinCan.Features.Interaction;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>Entities: stable ids, actor ids derived from them, and registration once per entity (plan network-entities.md).</summary>
    public class EntityTests
    {
        private static readonly Type AbilityMediatorType =
            Type.GetType("TinCan.Network.Infrastructure.Abilities.AbilityNetworkMediator, Assembly-CSharp", true)!;

        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in _objects) if (instance != null) Object.DestroyImmediate(instance);
            _objects.Clear();
        }

        [Test]
        public void Derive_RootIsTheEntityId_ChildrenAreStableAndDistinct()
        {
            var entity = Guid.NewGuid();

            Assert.That(EntityIds.Derive(entity, ""), Is.EqualTo(entity));
            Assert.That(EntityIds.Derive(entity, "Sockets/Socket_1"), Is.EqualTo(EntityIds.Derive(entity, "Sockets/Socket_1")), "same on every peer");
            Assert.That(EntityIds.Derive(entity, "Sockets/Socket_1"), Is.Not.EqualTo(EntityIds.Derive(entity, "Sockets/Socket_2")));
            Assert.That(EntityIds.Derive(entity, "Sockets/Socket_1"), Is.Not.EqualTo(EntityIds.Derive(Guid.NewGuid(), "Sockets/Socket_1")));
        }

        [Test]
        public void NetworkEntityId_RoundTripsTheGuid()
        {
            var id = Guid.NewGuid();
            Assert.That(new NetworkEntityId(id).Value, Is.EqualTo(id));
            Assert.That(new NetworkEntityId(Guid.Empty).IsEmpty, Is.True);
        }

        [Test]
        public void ActorIds_FollowTheEntity_AndComponentsOnOneObjectShareOne()
        {
            var root = Create("Ship");
            var entity = root.AddComponent<FakeEntity>();
            var rootActor = root.AddComponent<FuelTankNetworkMediator>();
            var sibling = (IActor)root.AddComponent(AbilityMediatorType);
            var socket = Create("Socket_1", Create("Sockets", root));
            var childActor = (IActor)socket.AddComponent(AbilityMediatorType);

            Assert.That(rootActor.Id, Is.EqualTo(entity.EntityId));
            Assert.That(sibling.Id, Is.EqualTo(entity.EntityId), "whatever the component order");
            Assert.That(childActor.Id, Is.EqualTo(EntityIds.Derive(entity.EntityId, "Sockets/Socket_1")));
        }

        [Test]
        public void ActorIds_WaitForTheEntityId_AndFallBackToALocalIdWithoutEntity()
        {
            var root = Create("Client object");
            var entity = root.AddComponent<FakeEntity>();
            entity.EntityId = Guid.Empty; // a client before the spawn arrives
            var actor = root.AddComponent<FuelTankNetworkMediator>();
            Assert.That(actor.Id, Is.EqualTo(Guid.Empty));
            entity.EntityId = Guid.NewGuid();
            Assert.That(actor.Id, Is.EqualTo(entity.EntityId), "not cached while empty");

            var loose = Create("Local only").AddComponent<FuelTankNetworkMediator>();
            Assert.That(loose.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(loose.Id, Is.EqualTo(loose.Id), "stable");
        }

        [Test]
        public void EntityRegistry_RegistersOncePerId()
        {
            var registry = new EntityRegistry();
            var first = Create("A").AddComponent<FakeEntity>();
            var twin = Create("B").AddComponent<FakeEntity>();
            twin.EntityId = first.EntityId;
            var unassigned = Create("C").AddComponent<FakeEntity>();
            unassigned.EntityId = Guid.Empty;

            Assert.That(registry.Register(first), Is.True);
            Assert.That(registry.Register(first), Is.False);
            Assert.That(registry.Register(twin), Is.False, "the id is taken");
            Assert.That(registry.Register(unassigned), Is.False, "no id yet");
            Assert.That(registry.Unregister(twin), Is.False, "only the registered instance can leave");
            Assert.That(registry.Unregister(first), Is.True);
            Assert.That(registry.All, Is.Empty);
        }

        [Test]
        public void Orchestrator_RegistersAnEntityOnce_WithOneActorAndOneAbilityController()
        {
            var actors = new FakeActorRegistry();
            var abilities = new FakeAbilityRegistry();
            var type = Type.GetType("TinCan.Core.Infrastructure.ActorOrchestrator, Assembly-CSharp", true)!;
            var orchestrator = (IActorOrchestrator)Activator.CreateInstance(type, actors, new InteractorRegistry(), abilities)!;

            var root = Create("Ship");
            var entity = root.AddComponent<FakeEntity>();
            var tank = root.AddComponent<FuelTankNetworkMediator>();
            root.AddComponent(AbilityMediatorType);

            orchestrator.RegisterEntity(entity);
            orchestrator.RegisterEntity(entity);

            Assert.That(actors.AllActors, Is.EqualTo(new IActor[] { tank }));
            Assert.That(abilities.AllControllers.Count(), Is.EqualTo(1));
            Assert.That(orchestrator.Entities.All, Is.EqualTo(new IEntity[] { entity }));

            orchestrator.UnregisterEntity(entity);
            Assert.That(actors.AllActors, Is.Empty);
            Assert.That(abilities.AllControllers, Is.Empty);
            Assert.That(orchestrator.Entities.All, Is.Empty);
        }

        [Test]
        public void Possession_IsOptIn_OnlyPlayerShipAndFreeCameraArePossessable()
        {
            var possessable = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name is "Assembly-CSharp" or "TinCan.Features")
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IPossessable).IsAssignableFrom(t))
                .Select(t => t.Name)
                .OrderBy(n => n);

            Assert.That(possessable, Is.EqualTo(new[] { "AirshipNetworkMediator", "FreeCameraTransformView", "HumanoidPlayer" }),
                "Only objects with a PossessableNetworkMediator (or the local free camera) may be possession candidates.");
        }

        private GameObject Create(string name, GameObject? parent = null)
        {
            var instance = new GameObject(name);
            if (parent != null) instance.transform.SetParent(parent.transform);
            _objects.Add(instance);
            return instance;
        }
    }
}
