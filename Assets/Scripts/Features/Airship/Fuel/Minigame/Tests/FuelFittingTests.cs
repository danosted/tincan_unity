#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Airship.Fuel.Minigame;
using UnityEditor;
using UnityEngine;
using TinCan.Features.Airship.Fuel.Tests;
using Object = UnityEngine.Object;

namespace TinCan.Features.Airship.Fuel.Minigame.Tests
{
    /// <summary>
    /// Fuel through the sockets (.docs/plans/main-game-state.md, M2): the tank is ship state on its own fixture, the
    /// receiver and the crystal rack are fittings (and still the old hull's FuelSystem), and parts on a ship find the
    /// ship's tank wherever it sits.
    /// </summary>
    public class FuelFittingTests
    {
        private const string Parts = "Assets/Prefabs/Airship/Parts/";
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in _objects) if (instance != null) Object.DestroyImmediate(instance);
            _objects.Clear();
        }

        [Test]
        public void APartOnAShip_FindsTheShipsTank_InASiblingFixture()
        {
            var ship = Make("Ship");
            ship.AddComponent(Type.GetType("TinCan.Network.Infrastructure.AirshipNetworkMediator, TinCan.Network", true)!);
            var tank = FakeFuelTankBehaviour.AttachTo(ship, null);
            var receiver = Make("FuelReceiver");
            receiver.transform.SetParent(ship.transform, false);
            var port = Make("MotorFillPort").AddComponent<MotorFillPortNetworkMediator>();
            port.transform.SetParent(receiver.transform, false);

            Assert.That(FuelTankLocator.FindFor(port), Is.SameAs(tank));
            Assert.That(port.Tank, Is.SameAs(tank));
        }

        [Test]
        public void APartOffAShip_FindsATankAboveIt()
        {
            var root = Make("FuelSystem");
            var tank = root.AddComponent<FakeFuelTankBehaviour>();
            var child = Make("MotorFillPort");
            child.transform.SetParent(root.transform, false);

            Assert.That(FuelTankLocator.FindFor(child.transform), Is.SameAs(tank));
        }

        [Test]
        public void TheTank_IsShipState_AndTheOldFuelSystemNoLongerCarriesIt()
        {
            var fuel = Load<FuelFeatureInstaller>("Assets/Resources/Installers/FuelFeatureInstaller.asset");
            var fixtures = ((FeatureInstaller.IExtension<ShipFixtureDefinition>)fuel).Contributions.ToList();
            var state = fixtures.Single(f => f.Prefab != null && f.Prefab.name == "FuelTankState");

            Assert.That(state.LocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(state.Prefab!.GetComponent<FuelTankNetworkMediator>(), Is.Not.Null);
            Assert.That(state.Prefab.GetComponent<FuelTankNetworkMediator>().Config, Is.Not.Null);
            Assert.That(fuel.NetworkedPrefabs, Does.Contain(state.Prefab));
            Assert.That(Load<GameObject>(Parts + "FuelSystem.prefab").GetComponentsInChildren<FuelTankNetworkMediator>(true), Is.Empty);
        }

        [TestCase("fuel.receiver", "FuelReceiver")]
        [TestCase("fuel.crystal_rack", "CrystalRack")]
        public void FuelContributes_TheFitting_WithItsOwnNetworkObject(string fittingId, string prefabName)
        {
            var fuel = Load<FuelFeatureInstaller>("Assets/Resources/Installers/FuelFeatureInstaller.asset");
            var fitting = ((FeatureInstaller.IExtension<ShipFittingDefinition>)fuel).Contributions.Single(f => f.FittingId == fittingId);

            Assert.That(fitting.Prefab, Is.Not.Null);
            Assert.That(fitting.Prefab!.name, Is.EqualTo(prefabName));
            Assert.That(fitting.Prefab.GetComponent<Unity.Netcode.NetworkObject>(), Is.Not.Null);
            Assert.That(fuel.NetworkedPrefabs, Does.Contain(fitting.Prefab));
        }

        [Test]
        public void EveryCrystalRack_ReadsTheFuelConfig_ForItsCanItem()
        {
            foreach (var path in new[] { Parts + "CrystalRack.prefab", Parts + "FuelSystem.prefab" })
            {
                var supply = Load<GameObject>(path).GetComponentInChildren<JerryCanSupplyNetworkMediator>(true);
                Assert.That(supply, Is.Not.Null, path);
                Assert.That(supply.Item, Is.Not.Null, $"{path}: a fitting is spawned before it is parented, so it cannot read the tank's config");
            }
        }

        [Test]
        public void TheMinigame_ContributesTheNetRack_AsAFitting()
        {
            var minigame = Load<FlyingCanFeatureInstaller>("Assets/Resources/Installers/FlyingCanFeatureInstaller.asset");
            var fitting = ((FeatureInstaller.IExtension<ShipFittingDefinition>)minigame).Contributions.Single();

            Assert.That(fitting.FittingId, Is.EqualTo("fuel.net_rack"));
            Assert.That(fitting.Prefab, Is.SameAs(Load<GameObject>(Parts + "NetRack.prefab")));
        }

        private GameObject Make(string name)
        {
            var instance = new GameObject(name);
            _objects.Add(instance);
            return instance;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }
    }
}
