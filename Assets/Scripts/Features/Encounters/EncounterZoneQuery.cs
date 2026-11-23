#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Encounters
{
    /// <summary>Every peer: <see cref="IEncounterZones"/> from the zones in the actor registry (replicated state).</summary>
    public sealed class EncounterZoneQuery : IEncounterZones
    {
        private readonly IActorRegistry _actors;

        public EncounterZoneQuery(IActorRegistry actors) => _actors = actors;

        public (IEncounterZone Zone, float Depth)? ZoneAt(Vector3 point)
        {
            (IEncounterZone Zone, float Depth)? best = null;
            foreach (var zone in _actors.GetActors<IEncounterZone>())
            {
                if (!zone.IsSimulating) continue;
                float depth = EncounterZoneProcessor.Depth(zone.Centre, zone.Radius, point);
                if (depth > 0f && (best == null || depth > best.Value.Depth)) best = (zone, depth);
            }

            return best;
        }

        public IEncounterZone? Nearest(Vector3 point)
        {
            IEncounterZone? nearest = null;
            float best = float.PositiveInfinity;
            foreach (var zone in _actors.GetActors<IEncounterZone>())
            {
                if (!zone.IsSimulating) continue;
                float distance = Vector2.Distance(zone.Centre, new Vector2(point.x, point.z)) - zone.Radius;
                if (distance >= best) continue;
                best = distance;
                nearest = zone;
            }

            return nearest;
        }
    }
}
