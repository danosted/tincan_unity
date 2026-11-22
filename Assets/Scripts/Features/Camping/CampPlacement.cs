#nullable enable
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>Where a campfire or tent would stand if placed now, facing the player, and whether it fits there.</summary>
    public readonly struct CampPlacement
    {
        public readonly CampPlaceable Kind;
        public readonly CampPlacementVerdict Verdict;
        public readonly Vector3 Point;
        public readonly Quaternion Rotation;

        public CampPlacement(CampPlaceable kind, CampPlacementVerdict verdict, Vector3 point, Quaternion rotation)
        {
            Kind = kind;
            Verdict = verdict;
            Point = point;
            Rotation = rotation;
        }

        public bool Fits => Verdict == CampPlacementVerdict.Fits;

        /// <summary>Was there ground in reach at all (a preview to show, green or red)?</summary>
        public bool HasGround => Verdict != CampPlacementVerdict.NoGroundInReach;

        public override string ToString() => $"{Kind} {Verdict} at {Point}";
    }
}
