#nullable enable
using TinCan.Core.Ship;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A design's totals, the health they give, and how it flies: its core part's handling profile
    /// (<see cref="ShipStatsProcessor"/>; plan .docs/plans/ship-handling-profiles.md). Mass, lift and thrust do not
    /// change the flight for now; they are kept for the layers that come later.
    /// </summary>
    public readonly struct ShipStats
    {
        public ShipStats(float mass, float lift, float thrust, float hull, float maxHealth, ShipHandlingProfile? handling = null)
        {
            Mass = mass;
            Lift = lift;
            Thrust = thrust;
            Hull = hull;
            MaxHealth = maxHealth;
            Handling = handling;
        }

        public float Mass { get; }
        public float Lift { get; }
        public float Thrust { get; }
        public float Hull { get; }
        public float MaxHealth { get; }

        /// <summary>The core part's handling profile; null when the core names none (the ship keeps its prefab's).</summary>
        public ShipHandlingProfile? Handling { get; }

        public override string ToString() =>
            $"flies as {(Handling != null ? Handling.name : "its prefab")}, hull {MaxHealth:0} (mass {Mass:0}, lift {Lift:0}, thrust {Thrust:0})";
    }
}
