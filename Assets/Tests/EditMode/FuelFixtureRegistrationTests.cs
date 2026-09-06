#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Interaction;
using TinCan.Tests.EditMode.Fakes;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class FuelFixtureRegistrationTests
    {
        private sealed class RecordingAbilityRegistry : IAbilityRegistry
        {
            public readonly HashSet<IAbilityControllerBase> Controllers = new();
            public IEnumerable<IAbilityControllerBase> AllControllers => Controllers;
            public void Register(IAbilityControllerBase controller) => Controllers.Add(controller);
            public void Unregister(IAbilityControllerBase controller) => Controllers.Remove(controller);
        }

        private sealed class CountingModuleRegistry : IShipModuleRegistry
        {
            private readonly List<IShipModule> _modules = new();
            public IReadOnlyList<IShipModule> Modules => _modules;
            public int Registrations { get; private set; }
            public void RegisterModule(IShipModule module) { Registrations++; _modules.Add(module); }
            public void UnregisterModule(IShipModule module) => _modules.Remove(module);
        }

        private readonly List<GameObject> _objects = new();
        private FakeActorRegistry _actors = null!;
        private RecordingAbilityRegistry _abilities = null!;
        private IActorOrchestrator _orchestrator = null!;
        private FuelTankNetworkMediator _tank = null!;
        private FuelConfig _config = null!;
        private GameplayAttribute _fuel = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _abilities = new RecordingAbilityRegistry();
            // Infrastructure is in Assembly-CSharp; mirror the existing mediator tests' reflection boundary.
            var type = Type.GetType("TinCan.Core.Infrastructure.ActorOrchestrator, Assembly-CSharp", true)!;
            _orchestrator = (IActorOrchestrator)Activator.CreateInstance(type, _actors, new InteractorRegistry(), _abilities)!;
            _tank = CreateObject("Fuel fixture").AddComponent<FuelTankNetworkMediator>();
            _tank.Construct(_orchestrator);
            _config = ScriptableObject.CreateInstance<FuelConfig>();
            _fuel = AssetDatabase.LoadAssetAtPath<GameplayAttribute>(
                AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("Attr_Fuel t:GameplayAttribute")[0]));
            typeof(FuelTankNetworkMediator).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_tank, _config);
            typeof(FuelTankNetworkMediator).GetField("_fuelAttribute", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_tank, _fuel);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in _objects) if (instance != null) Object.DestroyImmediate(instance);
            _objects.Clear();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void SpawnAndDespawn_RegisterActorAndChildCapabilityThenCleanUp()
        {
            var child = CreateObject("Capability");
            child.transform.SetParent(_tank.transform);
            var controller = AddAbilityController(child);

            _tank.OnNetworkSpawn();

            Assert.That(_actors.GetActors<IShipModule>(), Does.Contain(_tank));
            Assert.That(_abilities.Controllers, Does.Contain(controller));

            _tank.OnNetworkDespawn();

            Assert.That(_actors.AllActors, Is.Empty);
            Assert.That(_abilities.Controllers, Is.Empty);
        }

        [Test]
        public void RegisterShipModule_RepeatedAttachmentAndReparenting_HasOneMembership()
        {
            var first = new CountingModuleRegistry();
            var second = new CountingModuleRegistry();
            _orchestrator.RegisterShipModule(_tank, first);
            _orchestrator.RegisterShipModule(_tank, first);
            Assert.That(first.Registrations, Is.EqualTo(1));

            _orchestrator.RegisterShipModule(_tank, second);
            Assert.That(first.Modules, Is.Empty);
            Assert.That(second.Modules, Is.EqualTo(new[] { _tank }));

            _orchestrator.UnregisterHierarchy(_tank.gameObject);
            _orchestrator.UnregisterShipModule(_tank);
            Assert.That(second.Modules, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Spawn_WithEarlyOrLateParent_BindsOnceAndCleansUp(bool parentBeforeSpawn)
        {
            var ship = CreateShip("Ship", 42f);
            if (parentBeforeSpawn) _tank.transform.SetParent(ship.transform);
            _tank.OnNetworkSpawn();
            if (!parentBeforeSpawn) ChangeParent(ship);
            _tank.OnAttachedToShip(ship.GetComponent<IActor>()); // Server also sends explicit attachment.

            Assert.That(ship.GetComponent<IShipModuleRegistry>().Modules, Is.EqualTo(new[] { _tank }));
            Assert.That(_tank.Level, Is.EqualTo(42f));

            _tank.OnNetworkDespawn();

            Assert.That(ship.GetComponent<IShipModuleRegistry>().Modules, Is.Empty);
            Assert.That(_actors.AllActors, Is.Empty);
            Assert.That(_tank.Level, Is.Zero);
        }

        [Test]
        public void ParentChange_RebindsFuelAndRemovesPreviousMembership()
        {
            var first = CreateShip("First", 42f);
            var second = CreateShip("Second", 17f);
            _tank.OnNetworkSpawn();
            ChangeParent(first);
            ChangeParent(second);

            Assert.That(first.GetComponent<IShipModuleRegistry>().Modules, Is.Empty);
            Assert.That(second.GetComponent<IShipModuleRegistry>().Modules, Is.EqualTo(new[] { _tank }));
            Assert.That(_tank.Level, Is.EqualTo(17f));

            ChangeParent(null);

            Assert.That(second.GetComponent<IShipModuleRegistry>().Modules, Is.Empty);
            Assert.That(_tank.Level, Is.Zero);
        }

        private GameObject CreateObject(string name)
        {
            var instance = new GameObject(name);
            _objects.Add(instance);
            return instance;
        }

        private IAbilityControllerBase AddAbilityController(GameObject instance)
        {
            var type = Type.GetType("TinCan.Network.Infrastructure.Abilities.AbilityNetworkMediator, Assembly-CSharp", true)!;
            var controller = (IAbilityControllerBase)instance.AddComponent(type);
            typeof(NetworkBehaviour).GetProperty("IsOwner")!.SetValue(controller, true);
            return controller;
        }

        private GameObject CreateShip(string name, float fuel)
        {
            var ship = CreateObject(name);
            var controller = AddAbilityController(ship);
            controller.SetAttribute(_fuel, new AttributeValue(fuel));
            var type = Type.GetType("TinCan.Network.Infrastructure.ShipModuleRegistryNetworkMediator, Assembly-CSharp", true)!;
            ship.AddComponent(type);
            return ship;
        }

        private void ChangeParent(GameObject? ship)
        {
            _tank.transform.SetParent(ship != null ? ship.transform : null);
            // Exercise the NGO callback without a transport; no RPC or serialized asset changes are needed.
            _tank.OnNetworkObjectParentChanged(ship != null ? ship.GetComponent<NetworkObject>() : null!);
        }
    }
}
