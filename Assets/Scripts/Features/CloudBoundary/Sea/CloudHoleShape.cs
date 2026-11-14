#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// A hole at one moment (<see cref="CloudHoleProcessor.Shape"/>): where it is, how wide its open part is, how wide the
    /// rim around it, and how open it is (0 shut, 1 fully open). What both the game and the sea shader read.
    /// </summary>
    public readonly struct CloudHoleShape
    {
        public CloudHoleShape(Vector2 centre, float radius, float rim, float openness)
        {
            Centre = centre;
            Radius = radius;
            Rim = rim;
            Openness = openness;
        }

        public Vector2 Centre { get; }
        public float Radius { get; }
        public float Rim { get; }
        public float Openness { get; }
    }
}
