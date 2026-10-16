#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>How hazard zones are laid along a voyage's route (from <see cref="SkyHazardConfig"/>).</summary>
    public readonly struct HazardZoneRules
    {
        public readonly float Spacing;
        public readonly Vector2 Radius;
        public readonly float LateralSpread;
        public readonly Vector2 HalfHeight;
        public readonly float AltitudeJitter;
        public readonly float KeepClear;

        public HazardZoneRules(float spacing, Vector2 radius, float lateralSpread, Vector2 halfHeight, float altitudeJitter, float keepClear)
        {
            Spacing = spacing;
            Radius = radius;
            LateralSpread = lateralSpread;
            HalfHeight = halfHeight;
            AltitudeJitter = altitudeJitter;
            KeepClear = keepClear;
        }
    }
}
