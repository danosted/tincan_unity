#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>One player's line as the server publishes it: who holds it, what it caught on, and the point in world space.</summary>
    public readonly struct GrappleLineSnapshot
    {
        public readonly IActor Player;
        public readonly Transform Frame;
        public readonly Vector3 WorldPoint;

        public GrappleLineSnapshot(IActor player, Transform frame, Vector3 worldPoint)
        {
            Player = player;
            Frame = frame;
            WorldPoint = worldPoint;
        }
    }
}
