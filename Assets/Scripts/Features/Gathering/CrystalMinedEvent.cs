#nullable enable
using System;

namespace TinCan.Features.Gathering
{
    /// <summary>Server: a player mined a crystal from a vein; <see cref="Left"/> is what the vein holds now.</summary>
    public readonly struct CrystalMinedEvent
    {
        public readonly Guid MinerId;
        public readonly int Left;

        public CrystalMinedEvent(Guid minerId, int left)
        {
            MinerId = minerId;
            Left = left;
        }
    }
}
