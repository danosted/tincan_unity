#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A corner of a ship's frame. It belongs to one story; <see cref="X"/> and <see cref="Z"/> place it on the story's
    /// floor plane and <see cref="Y"/> is its drift above (or below) that plane, so decks need not lie flat. All in frame
    /// units (<see cref="ShipFrameUnits"/>). <see cref="Id"/> comes from the design's shared id counter.
    /// </summary>
    public readonly struct ShipFrameNode
    {
        public ShipFrameNode(int id, int story, int x, int y, int z)
        {
            Id = id;
            Story = story;
            X = x;
            Y = y;
            Z = z;
        }

        public int Id { get; }
        public int Story { get; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public ShipFrameNode MovedTo(int x, int y, int z) => new(Id, Story, x, y, z);

        public override string ToString() => $"node #{Id} (story {Story}, {X}, {Y}, {Z})";
    }
}
