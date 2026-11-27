#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// How a design's totals are counted. How a ship flies is its core's handling profile, not its parts (plan
    /// .docs/plans/ship-handling-profiles.md).
    /// </summary>
    [Serializable]
    public struct ShipStatsTuning
    {
        [Tooltip("Health of a ship whose parts add none.")]
        [Min(1f)] public float MinHealth;
        [Tooltip("The mass a ship's spirit holds up when its design has a cairn, counted in its lift. Nothing limits a " +
                 "ship by lift for now; the limit returns with the spirit's talents (.docs/plans/spirit-talents.md).")]
        [Min(0f)] public float SpiritLift;

        public static ShipStatsTuning Default => new()
        {
            MinHealth = 100f,
            SpiritLift = 3000f,
        };
    }
}
