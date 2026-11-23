#nullable enable
using UnityEngine;

namespace TinCan.Features.Encounters
{
    /// <summary>
    /// Pure rules of an encounter zone's round (encounters.md, "The flow"), and where a point is in a zone. A ship
    /// crossing in starts the warning; turning back out during it goes back to idle; when it runs out the zone runs;
    /// sailing out abandons it, and then it rests and returns, or is gone, by the config. Success and failure come from
    /// the zone's event (E2) through <see cref="End"/>.
    /// </summary>
    public static class EncounterZoneProcessor
    {
        /// <summary>The next state and when it ends, from whether a ship is inside and the server time.</summary>
        public static (EncounterZoneState State, double EndsAt) Step(EncounterZoneState state, double endsAt, bool shipInside, double now,
            EncounterZoneConfig config) => state switch
        {
            EncounterZoneState.Idle when shipInside => (EncounterZoneState.Warning, now + config.WarningSeconds),
            EncounterZoneState.Warning when !shipInside => (EncounterZoneState.Idle, 0d),
            EncounterZoneState.Warning when now >= endsAt => (EncounterZoneState.Running, 0d),
            EncounterZoneState.Running when !shipInside => End(now, config),
            EncounterZoneState.Resting when now >= endsAt => (EncounterZoneState.Idle, 0d),
            _ => (state, endsAt)
        };

        /// <summary>A round ends (abandoned, cured or failed): it rests until a time, or is gone.</summary>
        public static (EncounterZoneState State, double EndsAt) End(double now, EncounterZoneConfig config) =>
            config.AfterEnd == EncounterAfterEnd.Gone
                ? (EncounterZoneState.Gone, 0d)
                : (EncounterZoneState.Resting, now + config.RestSeconds);

        /// <summary>Whether a point is inside the disc, at any height.</summary>
        public static bool Contains(Vector2 centre, float radius, Vector3 point) => Depth(centre, radius, point) > 0f;

        /// <summary>How deep a point is: 0 at the wall (and outside), 1 at the centre.</summary>
        public static float Depth(Vector2 centre, float radius, Vector3 point)
        {
            if (radius <= 0f) return 0f;
            float distance = Vector2.Distance(centre, new Vector2(point.x, point.z));
            return Mathf.Clamp01(1f - distance / radius);
        }
    }
}
