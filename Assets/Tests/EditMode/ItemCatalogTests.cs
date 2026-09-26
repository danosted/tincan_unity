#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Features.Items;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class ItemCatalogTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        private ItemDefinition Item(int id, string name)
        {
            var item = ItemDefinition.Create(id, name);
            _created.Add(item);
            return item;
        }

        [Test]
        public void TryGet_ResolvesById()
        {
            var tool = Item(3, "ITEM_RepairTool");
            var catalog = new ItemCatalog(new[] { Item(1, "ITEM_JerryCan"), tool });

            Assert.That(catalog.TryGet(3, out var found), Is.True);
            Assert.That(found, Is.SameAs(tool));
            Assert.That(catalog.Find(ItemCatalog.None), Is.Null);
            Assert.That(catalog.FindByName("ITEM_JerryCan")?.Id, Is.EqualTo(1));
            Assert.That(catalog.Problems, Is.Empty);
        }

        [Test]
        public void DuplicateId_KeepsTheFirstAndReportsTheSecond()
        {
            var first = Item(2, "ITEM_Net");
            var clash = Item(2, "ITEM_Other");
            var catalog = new ItemCatalog(new[] { first, clash });

            Assert.That(catalog.Find(2), Is.SameAs(first));
            Assert.That(catalog.Contains(clash), Is.False);
            Assert.That(catalog.Problems, Has.Count.EqualTo(1).And.Some.Contains("ITEM_Other"));
        }

        [Test]
        public void NonPositiveIds_AreRejected()
        {
            var catalog = new ItemCatalog(new[] { Item(0, "ITEM_Zero"), Item(-4, "ITEM_Negative"), null });

            Assert.That(catalog.All, Is.Empty);
            Assert.That(catalog.Problems, Has.Count.EqualTo(2));
        }
    }
}
