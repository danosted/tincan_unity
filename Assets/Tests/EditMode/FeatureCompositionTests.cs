#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Tests.EditMode
{
    /// <summary>Covers the two pure pieces of feature composition: installer ordering and phase-grouped ticking.</summary>
    public class FeatureCompositionTests
    {
        private sealed class TestInstaller : FeatureInstaller
        {
            public int OrderValue;
            public override int Order => OrderValue;
            public override void Install(IContainerBuilder builder) { }
        }

        private sealed class Tickable : ISimulationTickable
        {
            public Tickable(SimulationPhase phase, List<string> log, string name) { Phase = phase; _log = log; _name = name; }
            private readonly List<string> _log;
            private readonly string _name;
            public SimulationPhase Phase { get; }
            public void Tick() => _log.Add(_name);
        }

        private readonly List<Object> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Catalog_OrdersInstallersByOrderThenName_AndDropsNulls()
        {
            var b = Installer("B", 0);
            var a = Installer("A", 0);
            var early = Installer("Z", -10);

            var catalog = new FeatureInstallerCatalog(new FeatureInstaller?[] { b, null, a, early }!);

            Assert.That(catalog.Installers, Is.EqualTo(new[] { early, a, b }));
            Assert.That(catalog.Fixtures, Is.Empty);
            Assert.That(catalog.NetworkedPrefabs, Is.Empty);
        }

        [Test]
        public void TickRunner_RunsOnlyTheRequestedPhase_InRegistrationOrder()
        {
            var log = new List<string>();
            var runner = new SimulationTickRunner(new ISimulationTickable[]
            {
                new Tickable(SimulationPhase.AfterHumanoid, log, "catch"),
                new Tickable(SimulationPhase.AfterAirship, log, "fuel"),
                new Tickable(SimulationPhase.AfterAirship, log, "cans"),
            });

            runner.Run(SimulationPhase.AfterAirship);
            Assert.That(log, Is.EqualTo(new[] { "fuel", "cans" }));

            runner.Run(SimulationPhase.AfterHumanoid);
            Assert.That(log, Is.EqualTo(new[] { "fuel", "cans", "catch" }));
            Assert.That(runner.Count, Is.EqualTo(3));
        }

        private TestInstaller Installer(string name, int order)
        {
            var installer = ScriptableObject.CreateInstance<TestInstaller>();
            installer.name = name;
            installer.OrderValue = order;
            _assets.Add(installer);
            return installer;
        }
    }
}
