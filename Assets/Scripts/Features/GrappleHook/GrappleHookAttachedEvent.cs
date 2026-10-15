#nullable enable
using System;

namespace TinCan.Features.GrappleHook
{
    /// <summary>Server: a player's grappling hook caught on something.</summary>
    public readonly struct GrappleHookAttachedEvent
    {
        public readonly Guid PlayerId;
        public readonly string Target;
        public readonly float Distance;

        public GrappleHookAttachedEvent(Guid playerId, string target, float distance)
        {
            PlayerId = playerId;
            Target = target;
            Distance = distance;
        }

        public override string ToString() => $"GrappleHookAttached player={PlayerId} target={Target} distance={Distance:0.0}m";
    }
}
