#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Gates
{
    /// <summary>Server: the gates in the sky. A feature that lays gates (boons along the route) spawns and removes them here.</summary>
    public interface IGates
    {
        /// <summary>Gates spawned and not yet removed, used or not.</summary>
        IReadOnlyList<IGate> Gates { get; }

        /// <summary>Gates the ship passed through since the host started.</summary>
        int Passed { get; }

        /// <summary>A gate of this kind here; null off the server or when it cannot spawn.</summary>
        IGate? Spawn(GateDefinition definition, Vector3 position);

        void Despawn(IGate gate);
    }
}
