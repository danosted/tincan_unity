#nullable enable
using NUnit.Framework;
using UnityEngine;

namespace TinCan.Core.Items.Tests
{
    /// <summary>A stock drawn as one child per item (<see cref="StockVisuals"/>).</summary>
    public class StockVisualsTests
    {
        private GameObject _root = null!;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Bin");
            for (int i = 0; i < 3; i++) new GameObject($"Stock_{i:00}").transform.SetParent(_root.transform);
            new GameObject("Crate").transform.SetParent(_root.transform);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void ItShows_OnePerItem_AndOnlyItsOwnChildren()
        {
            var stock = new StockVisuals(_root.transform, "Stock_");

            stock.Show(2);

            Assert.That(stock.Length, Is.EqualTo(3));
            Assert.That(_root.transform.Find("Stock_00").gameObject.activeSelf, Is.True);
            Assert.That(_root.transform.Find("Stock_01").gameObject.activeSelf, Is.True);
            Assert.That(_root.transform.Find("Stock_02").gameObject.activeSelf, Is.False);
            Assert.That(_root.transform.Find("Crate").gameObject.activeSelf, Is.True, "not part of the stock");
        }
    }
}
