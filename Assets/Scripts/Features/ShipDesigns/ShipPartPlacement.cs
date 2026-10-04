#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// One part placed in a design. <see cref="InstanceId"/> is the placement's own id, unique in its design and never
    /// reused, so state that later hangs off a part (damage, wiring, settings) survives edits and saves.
    /// <see cref="PartId"/> names the part type (<c>ShipPartDefinition.PartId</c>), never an asset name or GUID.
    /// <see cref="Extensions"/> holds fields this build does not know, as raw JSON, so a file from a later build keeps
    /// them through a load and save.
    /// </summary>
    public sealed class ShipPartPlacement
    {
        private static readonly IReadOnlyDictionary<string, string> NoExtensions = new Dictionary<string, string>();

        public ShipPartPlacement(int instanceId, string partId, ShipGridCell cell, byte orientation,
            IReadOnlyDictionary<string, string>? extensions = null)
        {
            InstanceId = instanceId;
            PartId = partId;
            Cell = cell;
            Orientation = orientation;
            Extensions = extensions ?? NoExtensions;
        }

        public int InstanceId { get; }
        public string PartId { get; }
        public ShipGridCell Cell { get; }
        public byte Orientation { get; }
        public IReadOnlyDictionary<string, string> Extensions { get; }

        public override string ToString() => $"#{InstanceId} {PartId} at {Cell} orientation {Orientation}";
    }
}
