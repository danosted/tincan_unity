#nullable enable
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>The world, asked where a campfire or tent could go. A seam over physics, so placement stays testable.</summary>
    public interface ICampPlacementQuery
    {
        /// <summary>The first solid surface along the aim within <paramref name="reach"/>, never the player's own body.</summary>
        bool TryAim(Vector3 eye, Vector3 direction, float reach, Transform ignore, out CampGround ground);

        /// <summary>
        /// Is a disc of this radius standing on <paramref name="point"/> clear of anything solid (rock, trees, other
        /// camps, other players) up to <paramref name="height"/>, the player themselves aside?
        /// </summary>
        bool HasRoom(Vector3 point, float radius, float height, Transform ignore);
    }
}
