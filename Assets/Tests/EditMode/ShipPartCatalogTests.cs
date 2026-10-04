#nullable enable
using NUnit.Framework;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipPartCatalog"/>: lookup by id; malformed and duplicate ids are reported, not used.</summary>
    public class ShipPartCatalogTests
    {
        private ShipPartDefinition _block = null!, _copy = null!, _bad = null!;

        [SetUp]
        public void SetUp()
        {
            _block = ShipPartDefinition.Create("hull.block");
            _copy = ShipPartDefinition.Create("hull.block");
            _copy.name = "Copy";
            _bad = ShipPartDefinition.Create("Hull Block!");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_block);
            Object.DestroyImmediate(_copy);
            Object.DestroyImmediate(_bad);
        }

        [Test]
        public void Parts_AreFoundByTheirId()
        {
            var catalog = new ShipPartCatalog(new[] { _block });

            Assert.That(catalog.TryGet("hull.block", out var part), Is.True);
            Assert.That(part, Is.SameAs(_block));
            Assert.That(catalog.TryGet("hull.nope", out _), Is.False);
            Assert.That(catalog.Problems, Is.Empty);
        }

        [Test]
        public void TheFirstOfTwoPartsWithOneId_Wins_AndTheClashIsReported()
        {
            var catalog = new ShipPartCatalog(new[] { _block, _copy, _block });

            Assert.That(catalog.Parts, Is.EqualTo(new[] { _block }));
            Assert.That(catalog.Problems, Has.Count.EqualTo(1).And.Some.Contains("share the id"));
        }

        [Test]
        public void AMalformedId_IsLeftOut()
        {
            var catalog = new ShipPartCatalog(new[] { _bad, null });

            Assert.That(catalog.Parts, Is.Empty);
            Assert.That(catalog.Problems, Has.Some.Contains("Hull Block!"));
        }

        [TestCase("hull.block", true)]
        [TestCase("core.helm_2", true)]
        [TestCase("hull", true)]
        [TestCase("", false)]
        [TestCase("Hull.block", false)]
        [TestCase("hull..block", false)]
        [TestCase(".hull", false)]
        [TestCase("hull block", false)]
        public void PartIdFormat(string id, bool valid) => Assert.That(ShipPartDefinition.IsValidPartId(id), Is.EqualTo(valid));

        [Test]
        public void AnEmptyFootprint_IsTheOriginCell()
        {
            Assert.That(_block.Footprint, Is.EqualTo(new[] { Vector3Int.zero }));
        }
    }
}
