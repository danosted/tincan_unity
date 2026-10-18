#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A beam between two frame nodes: a floor edge when both are on one story, a post when they are on adjacent stories
    /// (posts may slant). <see cref="Rail"/> runs a railing or a gate along a floor edge, the whole edge long. Plans:
    /// .docs/plans/ship-frames.md, .docs/plans/frame-only-ships.md.
    /// </summary>
    public readonly struct ShipFrameEdge
    {
        public ShipFrameEdge(int id, int a, int b, ShipFrameRailing rail = ShipFrameRailing.None)
        {
            Id = id;
            A = a;
            B = b;
            Rail = rail;
        }

        public int Id { get; }
        public int A { get; }
        public int B { get; }
        public ShipFrameRailing Rail { get; }

        /// <summary>Something runs along it: a railing or a gate.</summary>
        public bool Railing => Rail != ShipFrameRailing.None;

        public bool Joins(int node) => A == node || B == node;

        public bool Joins(int a, int b) => (A == a && B == b) || (A == b && B == a);

        public int Other(int node) => A == node ? B : A;

        public ShipFrameEdge Between(int a, int b) => new(Id, a, b, Rail);

        public ShipFrameEdge WithRail(ShipFrameRailing rail) => new(Id, A, B, rail);

        public override string ToString() => $"edge #{Id} ({A}-{B}{(Railing ? $", {Rail.ToString().ToLowerInvariant()}" : string.Empty)})";
    }
}
