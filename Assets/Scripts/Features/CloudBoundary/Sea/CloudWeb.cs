#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The web of rifts that cuts the cloud sea into cloud islands (plan .docs/plans/cloud-sea-fog.md, F3): cells about
    /// <see cref="Cell"/> metres across, their ragged borders rifts <see cref="Width"/> metres wide rolling shut over
    /// <see cref="Rim"/> metres, about <see cref="OpenShare"/> of the borders open at a time, changing
    /// <see cref="Drift"/> times an hour, placed by <see cref="Offset"/> (from the world seed). Read by
    /// <see cref="CloudSeaFormula.WebCut"/>; off when <see cref="OpenShare"/> is 0.
    /// </summary>
    public readonly struct CloudWeb
    {
        public static readonly CloudWeb None = default;

        public CloudWeb(float cell, float width, float rim, float ragged, float openShare, float drift, Vector2 offset)
        {
            Cell = cell;
            Width = width;
            Rim = rim;
            Ragged = ragged;
            OpenShare = openShare;
            Drift = drift;
            Offset = offset;
        }

        public float Cell { get; }
        public float Width { get; }
        public float Rim { get; }
        public float Ragged { get; }
        public float OpenShare { get; }
        public float Drift { get; }
        public Vector2 Offset { get; }

        public bool IsOn => OpenShare > 0f && Cell > 1f;
    }
}
