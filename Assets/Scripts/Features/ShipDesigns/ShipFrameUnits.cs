#nullable enable
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Frame positions are whole numbers of 10 cm, so a frame is exact and the same on every peer (a game constant: a
    /// file with another unit would be a new format version).
    /// </summary>
    public static class ShipFrameUnits
    {
        public const float Size = 0.1f;
        public const int PerMetre = 10;

        public static float ToMetres(int units) => units * Size;

        public static int FromMetres(float metres) => Mathf.RoundToInt(metres / Size);
    }
}
