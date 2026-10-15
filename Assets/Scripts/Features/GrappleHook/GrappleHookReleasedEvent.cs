#nullable enable
using System;

namespace TinCan.Features.GrappleHook
{
    /// <summary>Server: a player's grappling hook let go (released, arrived, the line snapped, or the hook was put away).</summary>
    public readonly struct GrappleHookReleasedEvent
    {
        public readonly Guid PlayerId;
        public readonly string Reason;

        public GrappleHookReleasedEvent(Guid playerId, string reason)
        {
            PlayerId = playerId;
            Reason = reason;
        }

        public override string ToString() => $"GrappleHookReleased player={PlayerId} reason={Reason}";
    }
}
