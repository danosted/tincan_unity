#nullable enable
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Frame positions are whole numbers of 10 cm, so a frame is exact and the same on every peer (a game constant, like
    /// the grid's cell size). Grid cell n is frame unit n × <see cref="PerCell"/>.
    /// </summary>
    public static class ShipFrameUnits
    {
        public const float Size = 0.1f;
        public const int PerCell = 10;

        public static float ToMetres(int units) => units * Size;

        public static int FromMetres(float metres) => Mathf.RoundToInt(metres / Size);
    }
}
