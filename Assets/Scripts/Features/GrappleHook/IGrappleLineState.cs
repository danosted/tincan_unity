#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// The players' hook lines, replicated to every peer (late joiners included) so everyone sees who is hooked where. The
    /// server writes; the pull itself is simulated, never read from here. Lives on the GrappleHookState ship fixture.
    /// </summary>
    public interface IGrappleLineState : IActor
    {
        /// <summary>Server only: replaces the replicated lines.</summary>
        void ServerSetLines(IReadOnlyList<GrappleLineSnapshot> lines);

        /// <summary>Where <paramref name="player"/>'s line holds, in world space on this peer, if it has one.</summary>
        bool TryGetHookPoint(IActor player, out Vector3 worldPoint);
    }
}
