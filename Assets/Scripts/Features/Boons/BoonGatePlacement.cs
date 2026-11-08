#nullable enable
using UnityEngine;

namespace TinCan.Features.Boons
{
    /// <summary>Where one boon gate sits, and which of <see cref="BoonsConfig.Gates"/> it is.</summary>
    public readonly struct BoonGatePlacement
    {
        public readonly Vector3 Position;
        public readonly int GateIndex;

        public BoonGatePlacement(Vector3 position, int gateIndex)
        {
            Position = position;
            GateIndex = gateIndex;
        }
    }
}
