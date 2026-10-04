#nullable enable
using System;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>Server: a ship was given a design (it is now replicated and built on every peer).</summary>
    public readonly struct ShipDesignAppliedEvent
    {
        public readonly Guid ShipId;
        public readonly string Name;
        public readonly ulong Hash;
        public readonly int Parts;

        public ShipDesignAppliedEvent(Guid shipId, string name, ulong hash, int parts)
        {
            ShipId = shipId;
            Name = name;
            Hash = hash;
            Parts = parts;
        }

        public override string ToString() => $"Ship {ShipId} flies \"{Name}\" ({Parts} parts, hash {ShipDesignHash.ToText(Hash)})";
    }
}
