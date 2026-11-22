#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// The places on one island's top, in island space (<see cref="SkyIslandSiteProcessor"/>): its caves, and its
    /// clearings, open ground kept free of trees and rocks where players can camp (the middle first, then one inside each cave, then a landmass's
    /// others). A cave's clearing faces its mouth. Filled and reused between islands.
    /// </summary>
    public sealed class SkyIslandSites
    {
        public readonly List<SkyIslandCave> Caves = new();
        public readonly List<Pose> Clearings = new();

        public void Clear()
        {
            Caves.Clear();
            Clearings.Clear();
        }
    }
}
