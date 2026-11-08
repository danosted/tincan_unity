#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Boons
{
    /// <summary>
    /// Domain, pure: where a voyage's boon gates are. One every <see cref="BoonGateRules.Spacing"/> metres along the route,
    /// pushed off to one side by <see cref="BoonGateRules.LateralOffset"/> (never on the straight line, so going for a
    /// boon is a steering choice), a little up or down. None near the start or destination. The same seed gives the same
    /// gates on every peer (integer hashing, no shared random state).
    /// </summary>
    public class BoonGateLayoutProcessor
    {
        public void Gates(int seed, Vector3 origin, Vector3 destination, in BoonGateRules rules, List<BoonGatePlacement> into)
        {
            if (rules.GateCount <= 0 || rules.Spacing <= 0f) return;

            Vector3 route = destination - origin;
            Vector3 level = new(route.x, 0f, route.z);
            float length = level.magnitude;
            if (length < 1f) return;

            Vector3 forward = level / length;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            int count = Mathf.FloorToInt(length / rules.Spacing);
            for (int i = 1; i <= count; i++)
            {
                float t = i * rules.Spacing / length;
                float offset = Mathf.Lerp(rules.LateralOffset.x, rules.LateralOffset.y, Unit(seed, i, 0));
                float sign = Unit(seed, i, 1) < 0.5f ? -1f : 1f;
                float altitude = Mathf.Lerp(origin.y, destination.y, t) + Mathf.Lerp(-rules.AltitudeJitter, rules.AltitudeJitter, Unit(seed, i, 2));
                int gate = Mathf.Min(rules.GateCount - 1, Mathf.FloorToInt(Unit(seed, i, 3) * rules.GateCount));

                Vector3 position = origin + forward * (t * length) + side * (sign * offset);
                position.y = altitude;
                if (Near(position, origin, rules.KeepClear) || Near(position, destination, rules.KeepClear)) continue;
                into.Add(new BoonGatePlacement(position, gate));
            }
        }

        private static bool Near(Vector3 a, Vector3 b, float distance)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz < distance * distance;
        }

        // A value in [0, 1) from the seed, the gate and what is being chosen (the same hash as the hazard zones').
        private static float Unit(int seed, int gate, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)gate * 0x85EBCA77u ^ (uint)(salt + 17) * 0xC2B2AE3Du;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
