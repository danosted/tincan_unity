#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain.Navigation
{
    /// <summary>
    /// A place on the chart a route can end at: the voyage's destination, an island, treasure. Every peer must build the
    /// same POIs (from replicated or seeded state), since only their ids travel over the network.
    /// </summary>
    public readonly struct PointOfInterest
    {
        /// <summary>Stable across peers and for the whole session; not 0 (0 means "no POI" on a waypoint).</summary>
        public readonly int Id;

        public readonly string Name;
        public readonly Vector3 Position;

        /// <summary>On the chart from the start; otherwise hidden until the ship comes within <see cref="DiscoverRadius"/>.</summary>
        public readonly bool StartsKnown;

        public readonly float DiscoverRadius;

        public PointOfInterest(int id, string name, Vector3 position, bool startsKnown, float discoverRadius = 0f)
        {
            Id = id;
            Name = name;
            Position = position;
            StartsKnown = startsKnown;
            DiscoverRadius = discoverRadius;
        }

        public override string ToString() => $"{Name} #{Id} at {Position}";
    }
}
