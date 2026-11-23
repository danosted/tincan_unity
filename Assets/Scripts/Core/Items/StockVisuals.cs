#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Core.Items
{
    /// <summary>
    /// A stock drawn as one child per item there: the children of a root whose names start with a prefix (Crystal_0,
    /// Log_1, Stock_02, Can_3), in their order, shown while their index is below the count. Shared by every store and
    /// source that shows what it holds.
    /// </summary>
    public sealed class StockVisuals
    {
        private readonly List<GameObject> _items = new();
        private int _shown = -1;

        public StockVisuals(Transform root, string prefix)
        {
            foreach (Transform child in root)
            {
                if (child.name.StartsWith(prefix)) _items.Add(child.gameObject);
            }
        }

        /// <summary>How many there are to show.</summary>
        public int Length => _items.Count;

        /// <summary>Shows the first <paramref name="count"/>; does nothing when that is what shows already.</summary>
        public void Show(int count)
        {
            if (count == _shown) return;
            _shown = count;
            for (int i = 0; i < _items.Count; i++) _items[i].SetActive(i < count);
        }
    }
}
