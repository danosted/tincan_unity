#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// Server: forces a feature adds to a ship's flight on top of its engine (plan .docs/plans/cloud-sea.md, "Soaring and
    /// cloud shelves"): the cloud's lift, the speed a dive builds, the glide's sink, a storm's gusts.
    /// <see cref="AirshipMovementUseCase"/> keeps the ship's momentum (speed along its nose beyond the engine's) and asks
    /// each registered source every tick; with none, ships fly on their engine alone. Register with
    /// <c>.As&lt;IAirshipFlightForces&gt;()</c>.
    /// </summary>
    public interface IAirshipFlightForces
    {
        /// <param name="ship">The ship, at its pose this tick.</param>
        /// <param name="engineSpeed">Its engine's speed along the nose, m/s.</param>
        /// <param name="momentum">Its momentum now, m/s along the nose on top of the engine's (below zero slows it).</param>
        AirshipFlightForce Evaluate(IAirshipView ship, float engineSpeed, float momentum, float deltaTime);
    }

    /// <summary>What one <see cref="IAirshipFlightForces"/> source adds this tick.</summary>
    public readonly struct AirshipFlightForce
    {
        public AirshipFlightForce(float momentumChange, float vertical, float noseDrop, float level = 0f, Vector3 horizontal = default)
        {
            MomentumChange = momentumChange;
            Vertical = vertical;
            NoseDrop = noseDrop;
            Level = level;
            Horizontal = new Vector3(horizontal.x, 0f, horizontal.z);
        }

        /// <summary>A sideways push alone: a horizontal world velocity, m/s (its height is ignored).</summary>
        public static AirshipFlightForce Push(Vector3 horizontal) => new(0f, 0f, 0f, 0f, horizontal);

        /// <summary>Change of the momentum this tick, m/s.</summary>
        public float MomentumChange { get; }

        /// <summary>Vertical velocity added to the flight, m/s (below zero sinks).</summary>
        public float Vertical { get; }

        /// <summary>Pitch toward nose down, degrees per second (a stall).</summary>
        public float NoseDrop { get; }

        /// <summary>
        /// Where level is for the nose, degrees (above zero nose down; zero: the horizon): with no pitch input the nose
        /// eases there. The cloud sets it to the sea's slope, so a ship on the sea rides its swell.
        /// </summary>
        public float Level { get; }

        /// <summary>
        /// Horizontal world velocity added to the flight, m/s, whichever way the nose points (a storm's gust shoving the
        /// ship sideways). It blends in the way <see cref="Vertical"/> does, so the ship drifts with it while it lasts.
        /// </summary>
        public Vector3 Horizontal { get; }
    }
}
