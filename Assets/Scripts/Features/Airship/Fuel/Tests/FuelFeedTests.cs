#nullable enable
using NUnit.Framework;
using TinCan.Core.Items;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Tests
{
    /// <summary>What a held item is worth to the tank (<see cref="FuelFeed"/>): the receiver takes what is worth litres.</summary>
    public class FuelFeedTests
    {
        private FuelConfig _config = null!;
        private ItemDefinition _crystal = null!;
        private ItemDefinition _can = null!;
        private ItemDefinition _log = null!;

        [SetUp]
        public void SetUp()
        {
            _crystal = ItemDefinition.Create(8, "ITEM_FuelCrystal");
            _can = ItemDefinition.Create(1, "ITEM_JerryCan");
            _log = ItemDefinition.Create(5, "ITEM_Firewood");
            _config = ScriptableObject.CreateInstance<FuelConfig>();
            _config.CrystalItem = _crystal;
            _config.CrystalLitres = 10f;
            _config.JerryCanItem = _can;
            _config.JerryCanLitres = 25f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_crystal);
            Object.DestroyImmediate(_can);
            Object.DestroyImmediate(_log);
        }

        [Test]
        public void ACrystal_AndACan_AreWorthTheirLitres_AnythingElseNothing()
        {
            Assert.That(FuelFeed.Litres(_crystal, _config), Is.EqualTo(10f));
            Assert.That(FuelFeed.Litres(_can, _config), Is.EqualTo(25f));
            Assert.That(FuelFeed.Litres(_log, _config), Is.Zero);
        }

        [Test]
        public void WithoutCrystals_InTheConfig_ACrystalIsWorthNothing()
        {
            _config.CrystalItem = null;

            Assert.That(FuelFeed.Litres(_crystal, _config), Is.Zero);
        }
    }
}
