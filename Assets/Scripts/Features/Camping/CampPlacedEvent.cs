#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>A player set a campfire or tent down on the ground.</summary>
    public readonly struct CampPlacedEvent
    {
        public readonly Guid PlayerId;
        public readonly CampPlaceable Kind;
        public readonly Vector3 Point;

        public CampPlacedEvent(Guid playerId, CampPlaceable kind, Vector3 point)
        {
            PlayerId = playerId;
            Kind = kind;
            Point = point;
        }

        public override string ToString() => $"Player {PlayerId} placed a {Kind} at {Point}";
    }
}
