#nullable enable

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// A small generator that gives the same numbers on every peer, runtime and platform: integer hashing only
    /// (<see cref="System.Random"/> and Unity's noise make no such promise). Seed it from <see cref="Hash"/>.
    /// </summary>
    public struct SkyIslandRandom
    {
        private uint _state;

        public SkyIslandRandom(uint seed) => _state = seed;

        /// <summary>Mixes any number of integers into one well-spread seed.</summary>
        public static uint Hash(int a, int b = 0, int c = 0, int d = 0)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Mix(hash ^ (uint)a);
                hash = Mix(hash ^ (uint)b);
                hash = Mix(hash ^ (uint)c);
                hash = Mix(hash ^ (uint)d);
                return hash;
            }
        }

        /// <summary>The next 32 random bits.</summary>
        public uint NextUInt()
        {
            unchecked
            {
                _state += 0x9E3779B9u;
                return Mix(_state);
            }
        }

        /// <summary>In [0, 1).</summary>
        public float Value() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>In [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>In [min, max] (both inclusive).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextUInt() % (uint)(max - min + 1));

        private static uint Mix(uint z)
        {
            unchecked
            {
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                return z ^ (z >> 16);
            }
        }
    }
}
