#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>Server-side control over hazards, for scenarios and (later) designed events.</summary>
    public interface ISkyHazards
    {
        /// <summary>Hazards spawned and not yet removed.</summary>
        IReadOnlyList<ISkyHazard> Alive { get; }

        /// <summary>Hazards shot down since the host started.</summary>
        int Destroyed { get; }

        /// <summary>Hazards that reached the ship since the host started.</summary>
        int Hits { get; }

        /// <summary>A hazard here. It hangs still: a target to shoot, or something for the ship to hit.</summary>
        ISkyHazard? SpawnAt(Vector3 position);

        /// <summary>
        /// A moving hazard here: it flies on at <paramref name="velocity"/> (m/s, world) and leaves the sky after
        /// <paramref name="lifetime"/> seconds unless it hits the ship or is shot down first (a storm's debris on its wind).
        /// </summary>
        ISkyHazard? Launch(Vector3 position, Vector3 velocity, float lifetime);
    }
}
