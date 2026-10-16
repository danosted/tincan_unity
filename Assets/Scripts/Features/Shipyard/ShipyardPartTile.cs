#nullable enable
namespace TinCan.Features.Shipyard
{
    /// <summary>One part in the shipyard's building panel.</summary>
    public readonly struct ShipyardPartTile
    {
        public ShipyardPartTile(string partId, string name, string category, bool selected)
        {
            PartId = partId;
            Name = name;
            Category = category;
            Selected = selected;
        }

        public string PartId { get; }
        public string Name { get; }
        public string Category { get; }
        public bool Selected { get; }
    }
}
