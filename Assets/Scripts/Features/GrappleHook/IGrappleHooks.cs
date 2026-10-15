#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// The hooks this peer simulates: every player's on the server, the local player's on a client (predicted). Other
    /// players' lines reach a client through <see cref="IGrappleLineState"/>.
    /// </summary>
    public interface IGrappleHooks
    {
        /// <summary>Where <paramref name="playerId"/>'s hook holds now, in world space, while it pulls.</summary>
        bool TryGetHookPoint(Guid playerId, out Vector3 worldPoint);
    }
}
