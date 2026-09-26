#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Core.Domain
{
    public static class TransformSearch
    {
        /// <summary>
        /// Breadth-first search of <paramref name="root"/>'s descendants by name. Unlike <see cref="Transform.Find"/>, it
        /// still works after a visual is regrouped under an intermediate child (the player's Visual).
        /// </summary>
        public static Transform? FindDescendant(this Transform root, string name)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (Transform child in current)
                {
                    if (child.name == name) return child;
                    queue.Enqueue(child);
                }
            }

            return null;
        }
    }
}
