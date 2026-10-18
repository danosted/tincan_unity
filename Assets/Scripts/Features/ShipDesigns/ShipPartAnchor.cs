#nullable enable
using System;
using TinCan.Core.Ship.Parts;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// What a placed part hangs on: a plank floor face and a point on it (<see cref="X"/>, <see cref="Z"/>, frame units in
    /// ship space; the part stands at the floor's height there), or a frame node. The part follows its element when the
    /// frame changes, and is part of the ship because the element is. Plan: .docs/plans/frame-only-ships.md.
    /// </summary>
    public readonly struct ShipPartAnchor : IEquatable<ShipPartAnchor>
    {
        private ShipPartAnchor(ShipPartAttach kind, int element, int x, int z)
        {
            Kind = kind;
            Element = element;
            X = x;
            Z = z;
        }

        public ShipPartAttach Kind { get; }

        /// <summary>The face or node id.</summary>
        public int Element { get; }

        public int X { get; }
        public int Z { get; }

        public static ShipPartAnchor OnFace(int face, int x, int z) => new(ShipPartAttach.Face, face, x, z);

        public static ShipPartAnchor OnNode(int node) => new(ShipPartAttach.Node, node, 0, 0);

        public bool Equals(ShipPartAnchor other) => Kind == other.Kind && Element == other.Element && X == other.X && Z == other.Z;

        public override bool Equals(object? obj) => obj is ShipPartAnchor other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, Element, X, Z);

        public static bool operator ==(ShipPartAnchor a, ShipPartAnchor b) => a.Equals(b);

        public static bool operator !=(ShipPartAnchor a, ShipPartAnchor b) => !a.Equals(b);

        public override string ToString() => Kind == ShipPartAttach.Node ? $"on node #{Element}" : $"on face #{Element} at ({X}, {Z})";
    }
}
