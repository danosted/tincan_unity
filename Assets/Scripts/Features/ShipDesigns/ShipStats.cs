#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>A design's totals and the flight they give (<see cref="ShipStatsProcessor"/>).</summary>
    public readonly struct ShipStats
    {
        public ShipStats(float mass, float lift, float thrust, float hull, float maxSpeed, float turnSpeed, float maxHealth)
        {
            Mass = mass;
            Lift = lift;
            Thrust = thrust;
            Hull = hull;
            MaxSpeed = maxSpeed;
            TurnSpeed = turnSpeed;
            MaxHealth = maxHealth;
        }

        public float Mass { get; }
        public float Lift { get; }
        public float Thrust { get; }
        public float Hull { get; }
        public float MaxSpeed { get; }
        public float TurnSpeed { get; }
        public float MaxHealth { get; }

        public bool CanLift => Lift >= Mass;
        public bool HasThrust => Thrust > 0f;

        public override string ToString() =>
            $"mass {Mass:0}, lift {Lift:0}, thrust {Thrust:0}: {MaxSpeed:0.0} m/s, turns {TurnSpeed:0} deg/s, hull {MaxHealth:0}";
    }
}
