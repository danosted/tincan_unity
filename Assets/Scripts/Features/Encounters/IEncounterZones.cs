#nullable enable
using UnityEngine;

namespace TinCan.Features.Encounters
{
    /// <summary>
    /// Every peer: which encounter zone a point is in, and how deep (0 at the wall, 1 at the centre). How the HUD, the
    /// storm's look and, later, the zone's spawners keep to the zone.
    /// </summary>
    public interface IEncounterZones
    {
        /// <summary>The deepest zone holding <paramref name="point"/>, or null outside every zone.</summary>
        (IEncounterZone Zone, float Depth)? ZoneAt(Vector3 point);
    }
}
