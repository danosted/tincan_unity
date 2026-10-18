#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A flight of stairs in the frame: it hangs from <see cref="Edge"/>, a floor edge (the landing) on some story above the
    /// lowest, and runs down into the story below, as wide as the edge, square to it, on the side <see cref="Dir"/> says
    /// (+1: to the left of the edge from A to B seen from above; -1: to the right). Its run follows from the rise at a
    /// fixed slope (<see cref="ShipFrameTuning.StairSlope"/>). Plan: .docs/plans/frame-only-ships.md.
    /// </summary>
    public readonly struct ShipFrameStair
    {
        public ShipFrameStair(int id, int edge, int dir)
        {
            Id = id;
            Edge = edge;
            Dir = dir < 0 ? -1 : 1;
        }

        public int Id { get; }
        public int Edge { get; }
        public int Dir { get; }

        public ShipFrameStair Flipped() => new(Id, Edge, -Dir);

        public override string ToString() => $"stairs #{Id} (from edge {Edge}, side {(Dir > 0 ? "+" : "-")})";
    }
}
