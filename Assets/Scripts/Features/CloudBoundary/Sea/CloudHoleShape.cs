#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// A hole at one moment (<see cref="CloudHoleProcessor.Shape"/>): where it is, how wide its open part is, how wide the
    /// rim around it, and how open it is (0 shut, 1 fully open). A rift also has a reach: from its centre to one end of
    /// the line it is open along (zero for a round hole). What both the game and the sea shader read.
    /// </summary>
    public readonly struct CloudHoleShape
    {
        public CloudHoleShape(Vector2 centre, float radius, float rim, float openness, Vector2 reach = default, float ragged = 0f)
        {
            Centre = centre;
            Radius = radius;
            Rim = rim;
            Openness = openness;
            Reach = reach;
            Ragged = ragged;
        }

        public Vector2 Centre { get; }
        public float Radius { get; }
        public float Rim { get; }
        public float Openness { get; }
        public Vector2 Reach { get; }
        /// <summary>How much a rift's width wanders along it, 0 (straight edges) .. 1.</summary>
        public float Ragged { get; }
    }
}
