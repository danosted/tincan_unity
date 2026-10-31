#nullable enable
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>A breakable point a blast may reach: its index on the ship, where it is, and whether it sits beside a fitting.</summary>
    public readonly struct BlastTarget
    {
        public BlastTarget(int index, Vector3 position, bool isFitting)
        {
            Index = index;
            Position = position;
            IsFitting = isFitting;
        }

        public int Index { get; }
        public Vector3 Position { get; }
        public bool IsFitting { get; }
    }
}
