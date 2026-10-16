#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>
    /// Domain, pure: where a voyage's hazard zones are. Zones sit along the route from its start to its destination, one
    /// every <see cref="HazardZoneRules.Spacing"/> metres, pushed a little to either side and up or down, so the straight
    /// line runs through most of them and a navigator can plot around, over or under. What a seed gives is the same on
    /// every peer (integer hashing, no shared random state). No zone comes near the start or the destination.
    /// </summary>
    public class HazardZoneLayoutProcessor
    {
        public void Zones(int seed, Vector3 origin, Vector3 destination, in HazardZoneRules rules, List<HazardZone> into)
        {
            Vector3 route = destination - origin;
            Vector3 level = new(route.x, 0f, route.z);
            float length = level.magnitude;
            if (length < 1f || rules.Spacing <= 0f) return;

            Vector3 forward = level / length;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            int count = Mathf.FloorToInt(length / rules.Spacing);
            for (int i = 1; i <= count; i++)
            {
                float t = i * rules.Spacing / length;
                float radius = Mathf.Lerp(rules.Radius.x, rules.Radius.y, Unit(seed, i, 0));
                float lateral = Mathf.Lerp(-rules.LateralSpread, rules.LateralSpread, Unit(seed, i, 1));
                float halfHeight = Mathf.Lerp(rules.HalfHeight.x, rules.HalfHeight.y, Unit(seed, i, 2));
                float altitude = Mathf.Lerp(origin.y, destination.y, t) + Mathf.Lerp(-rules.AltitudeJitter, rules.AltitudeJitter, Unit(seed, i, 3));

                Vector3 center = origin + forward * (t * length) + side * lateral;
                center.y = altitude;
                if (Near(center, origin, radius + rules.KeepClear) || Near(center, destination, radius + rules.KeepClear)) continue;
                into.Add(new HazardZone(center, radius, altitude - halfHeight, altitude + halfHeight));
            }
        }

        /// <summary>Is the point inside any zone?</summary>
        public bool IsInside(Vector3 point, IReadOnlyList<HazardZone> zones)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].Contains(point)) return true;
            }
            return false;
        }

        private static bool Near(Vector3 a, Vector3 b, float distance)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz < distance * distance;
        }

        // A value in [0, 1) from the seed, the zone and what is being chosen.
        private static float Unit(int seed, int zone, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)zone * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du;
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
