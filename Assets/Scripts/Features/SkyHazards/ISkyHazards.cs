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

        bool FieldEnabled { get; set; }

        /// <summary>A hazard here. It hangs still (a target) unless <paramref name="drifts"/>, when it homes on the ship like the field's.</summary>
        ISkyHazard? SpawnAt(Vector3 position, bool drifts = false);
    }
}
