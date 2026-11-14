#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>The cloud sea's waves (<see cref="CloudSeaFormula.Swell"/>): how high, how long, how fast, and where the
    /// world seed puts them.</summary>
    public readonly struct CloudSwell
    {
        public static readonly CloudSwell None = new(0f, 1f, 0f, Vector2.zero);

        public CloudSwell(float amplitude, float wavelength, float speed, Vector2 offset)
        {
            Amplitude = amplitude;
            Wavelength = Mathf.Max(wavelength, 1f);
            Speed = speed;
            Offset = offset;
        }

        /// <summary>Metres from the base altitude to a crest.</summary>
        public float Amplitude { get; }

        /// <summary>Metres between crests of the longest wave.</summary>
        public float Wavelength { get; }

        /// <summary>Metres per second the waves roll.</summary>
        public float Speed { get; }

        /// <summary>Metres the pattern is shifted by the world seed.</summary>
        public Vector2 Offset { get; }

        public static Vector2 OffsetOf(int worldSeed) =>
            new((worldSeed & 0xFFFF) * 13.7f, ((worldSeed >> 16) & 0xFFFF) * 17.3f);
    }
}
