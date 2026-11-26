#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudMist
{
    /// <summary>
    /// How the sun's shadows reach out to a tier's shadow range, so the ship's and the islands' shadows land on the mist
    /// (plan .docs/plans/cloud-sea-fog.md, F4). The game's own shadow distance stays the first of four cascades, so the
    /// shadows on deck keep their sharpness; the others grow by an even ratio out to the range.
    /// </summary>
    public sealed class CloudMistShadowRange
    {
        public const int Cascades = 4;

        /// <summary>
        /// The four-cascade splits (shares of <paramref name="range"/>, as URP's cascade4Split) for reaching
        /// <paramref name="range"/> metres from the game's own <paramref name="gameDistance"/>, or null when the range does
        /// not reach past it (0, or shorter): then the game's own shadows stay as they are.
        /// </summary>
        public Vector3? Splits(float gameDistance, float range)
        {
            if (gameDistance <= 0f || range <= gameDistance) return null;
            float ratio = gameDistance / range;
            return new Vector3(ratio, Mathf.Pow(ratio, 2f / 3f), Mathf.Pow(ratio, 1f / 3f));
        }
    }
}
