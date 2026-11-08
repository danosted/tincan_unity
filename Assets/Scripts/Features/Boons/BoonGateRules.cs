#nullable enable
using UnityEngine;

namespace TinCan.Features.Boons
{
    /// <summary>The layout rules from <see cref="BoonsConfig"/>.</summary>
    public readonly struct BoonGateRules
    {
        public readonly float Spacing;
        public readonly Vector2 LateralOffset;
        public readonly float AltitudeJitter;
        public readonly float KeepClear;
        /// <summary>How many gates to pick from (<see cref="BoonsConfig.Gates"/>).</summary>
        public readonly int GateCount;

        public BoonGateRules(float spacing, Vector2 lateralOffset, float altitudeJitter, float keepClear, int gateCount)
        {
            Spacing = spacing;
            LateralOffset = lateralOffset;
            AltitudeJitter = altitudeJitter;
            KeepClear = keepClear;
            GateCount = gateCount;
        }
    }
}
