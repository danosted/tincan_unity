#nullable enable
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>Where the cursor points in parts mode: the frame element a new part would hang on, and the part under it.</summary>
    public readonly struct ShipyardAim
    {
        public ShipyardAim(ShipPartAnchor anchor, Vector3? partPoint)
        {
            IsValid = true;
            Anchor = anchor;
            PartPoint = partPoint;
        }

        private ShipyardAim(Vector3? partPoint)
        {
            IsValid = false;
            Anchor = default;
            PartPoint = partPoint;
        }

        /// <summary>True when the cursor is over something the selected part can hang on.</summary>
        public bool IsValid { get; }

        public ShipPartAnchor Anchor { get; }

        /// <summary>A ship-space point just inside what the cursor hits (what Remove takes), or null over nothing.</summary>
        public Vector3? PartPoint { get; }

        public static ShipyardAim None => default;

        public static ShipyardAim Nowhere(Vector3? partPoint) => new(partPoint);

        public override string ToString() => $"{(IsValid ? $"place {Anchor}" : "place nowhere")}, on {PartPoint?.ToString() ?? "nothing"}";
    }
}
