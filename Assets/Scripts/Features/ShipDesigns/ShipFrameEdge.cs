#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A beam between two frame nodes: a floor edge when both are on one story, a post when they are on adjacent stories
    /// (posts may slant). <see cref="Railing"/> puts a railing along a floor edge. Plan: .docs/plans/ship-frames.md.
    /// </summary>
    public readonly struct ShipFrameEdge
    {
        public ShipFrameEdge(int id, int a, int b, bool railing = false)
        {
            Id = id;
            A = a;
            B = b;
            Railing = railing;
        }

        public int Id { get; }
        public int A { get; }
        public int B { get; }
        public bool Railing { get; }

        public bool Joins(int node) => A == node || B == node;

        public bool Joins(int a, int b) => (A == a && B == b) || (A == b && B == a);

        public int Other(int node) => A == node ? B : A;

        public ShipFrameEdge Between(int a, int b) => new(Id, a, b, Railing);

        public override string ToString() => $"edge #{Id} ({A}-{B}{(Railing ? ", railing" : string.Empty)})";
    }
}
