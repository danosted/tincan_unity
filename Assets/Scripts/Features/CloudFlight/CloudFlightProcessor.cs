#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.CloudFlight
{
    /// <summary>
    /// The cloud flight in one step (plan .docs/plans/cloud-sea.md, "Soaring and cloud shelves"): charge in the cloud,
    /// launch out of it, soar, and come home. Pure; the speed and height trade is gravity along the path, made generous by
    /// the cloud.
    /// <list type="bullet">
    /// <item>The nose's slope pulls on the momentum: a dive builds it (much faster in the cloud), a climb steeper than the
    /// engine's free climb spends it (little of it in the cloud). It fades by drag, and the ship's speed stays between 0
    /// and the most.</item>
    /// <item>On the sea the cloud carries the ship to its rest height, moving or not, so it rides the swell: pushed in
    /// deeper, it is pushed out harder.</item>
    /// <item>Off the sea there is no updraft: the pull towards it builds, up to a slow fall at speed (the glide), a fast
    /// one when stalled, faster where the sky thins. Back on the sea the cloud takes the fall away. A stalled ship's
    /// raised nose drops back to level.</item>
    /// </list>
    /// </summary>
    public class CloudFlightProcessor
    {
        /// <param name="slope">The nose's slope, -1 straight down .. 1 straight up (its forward vector's y).</param>
        /// <param name="height">The ship's height above its rest height over the cloud, in metres (below zero: in the cloud).</param>
        /// <param name="fall">How fast the ship was falling, m/s (the last step's <see cref="CloudFlightReading.Fall"/>).</param>
        public AirshipFlightForce Evaluate(float slope, float height, float engineSpeed, float momentum, float fall,
            float deltaTime, CloudFlightConfig config) =>
            Read(slope, height, engineSpeed, momentum, fall, deltaTime, config).Force;

        /// <summary>The same step, with the parts of it a dashboard shows (and the fall, for the next step).</summary>
        public CloudFlightReading Read(float slope, float height, float engineSpeed, float momentum, float fall,
            float deltaTime, CloudFlightConfig config)
        {
            float inCloud = Mathf.Clamp01(-height / config.CloudBlend + 0.5f);
            float air = 1f - inCloud;

            float freeClimb = Mathf.Sin(config.FreeClimb * Mathf.Deg2Rad);
            float cost = slope < 0f ? slope : Mathf.Max(slope - freeClimb, 0f);
            float gain = slope < 0f ? Mathf.Lerp(1f, config.DiveGain, inCloud) : Mathf.Lerp(1f, config.ClimbCost, inCloud);
            float pull = -config.Pull * cost * gain;
            float drag = Mathf.Lerp(config.AirDrag, config.CloudDrag, inCloud);
            float next = momentum + (pull - drag * momentum) * deltaTime;

            float engine = Mathf.Max(engineSpeed, 0f);
            next = Mathf.Clamp(next, -engine, Mathf.Max(config.MaxSpeed - engine, 0f));
            float speed = engine + next;

            float stall = 1f - Smooth(0f, config.StallSpeed, speed);
            float thin = Mathf.Clamp01((height - config.CeilingStart) / config.CeilingDepth);

            // Off the sea the pull builds towards the fastest fall; back on it the cloud takes the fall away.
            float terminal = air * (Mathf.Lerp(config.GlideFall, config.StallFall, stall) + thin * config.CeilingSink);
            float rate = fall < terminal ? config.Gravity * air : config.CloudCatch;
            float nextFall = Mathf.MoveTowards(Mathf.Max(fall, 0f), terminal, rate * deltaTime);

            // On the sea the cloud carries the ship back to its rest height, so it rides the swell.
            float heave = inCloud * Mathf.Clamp(-config.Buoyancy * height, -config.MaxHeave, config.MaxHeave);
            float vertical = heave - nextFall;
            float noseDrop = air * stall * config.NoseDrop * Mathf.Clamp01(slope / 0.1f);

            var force = new AirshipFlightForce(next - momentum, vertical, noseDrop);
            return new CloudFlightReading(force, slope, height, inCloud, engine, next, stall, thin, heave, nextFall, deltaTime);
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / Mathf.Max(to - from, 1e-5f));
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>One step of the cloud flight for one ship, as the dashboard shows it. Momentum is after the step.</summary>
    public readonly struct CloudFlightReading
    {
        public readonly AirshipFlightForce Force;
        public readonly float Slope;
        public readonly float Height;
        public readonly float InCloud;
        public readonly float Engine;
        public readonly float Momentum;
        public readonly float Stall;
        public readonly float Thin;
        /// <summary>The cloud's lift (above zero) or settle (below), m/s.</summary>
        public readonly float Heave;
        /// <summary>How fast the pull towards the sea makes the ship fall, m/s (zero on the sea).</summary>
        public readonly float Fall;
        public readonly float DeltaTime;

        public CloudFlightReading(AirshipFlightForce force, float slope, float height, float inCloud, float engine,
            float momentum, float stall, float thin, float heave, float fall, float deltaTime)
        {
            Heave = heave;
            Fall = fall;
            Force = force;
            Slope = slope;
            Height = height;
            InCloud = inCloud;
            Engine = engine;
            Momentum = momentum;
            Stall = stall;
            Thin = thin;
            DeltaTime = deltaTime;
        }

        public float Speed => Engine + Momentum;

        /// <summary>How fast the momentum changes, m/s² (above zero: charging, below: spending).</summary>
        public float Charge => DeltaTime > 0f ? Force.MomentumChange / DeltaTime : 0f;
    }
}
