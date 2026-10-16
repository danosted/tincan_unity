#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A ship design: the one model the shipyard edits, files store, the network carries and the game builds a ship from.
    /// Catalog parts on the grid, and a <see cref="Frame"/> (stories, nodes, beams, plank faces) the hull is generated
    /// from. Immutable; edits make a new design (<see cref="ShipDesignEditProcessor"/>). Parts are kept in
    /// <see cref="ShipPartPlacement.InstanceId"/> order, so a design always encodes to the same bytes.
    /// Plans: .docs/plans/modular-airship-builder.md, .docs/plans/ship-frames.md.
    /// </summary>
    public sealed class ShipDesign
    {
        private static readonly IReadOnlyDictionary<string, string> NoExtensions = new Dictionary<string, string>();

        public ShipDesign(string name, string author, int nextPartInstanceId, IEnumerable<ShipPartPlacement> parts,
            IReadOnlyDictionary<string, string>? extensions = null, ShipFrame? frame = null)
        {
            Name = name;
            Author = author;
            Parts = parts.OrderBy(p => p.InstanceId).ToList();
            NextPartInstanceId = nextPartInstanceId;
            Extensions = extensions ?? NoExtensions;
            Frame = frame ?? ShipFrame.Empty;
        }

        public string Name { get; }
        public string Author { get; }

        /// <summary>
        /// The id the next part or frame element gets (parts, nodes, edges and faces share one counter, file field
        /// <c>nextId</c>). Ids are never reused, even after removal.
        /// </summary>
        public int NextPartInstanceId { get; }

        public IReadOnlyList<ShipPartPlacement> Parts { get; }

        /// <summary>The frame the hull is generated from; <see cref="ShipFrame.Empty"/> for a design built only from parts.</summary>
        public ShipFrame Frame { get; }

        /// <summary>Top-level fields this build does not know, as raw JSON (see <see cref="ShipPartPlacement.Extensions"/>).</summary>
        public IReadOnlyDictionary<string, string> Extensions { get; }

        /// <summary>The lowest id above every part and frame element: what the next id must at least be.</summary>
        public int IdFloor => System.Math.Max(Parts.Count == 0 ? 1 : Parts.Max(p => p.InstanceId) + 1,
            Frame.ElementCount == 0 ? 1 : Frame.Ids.Max() + 1);

        public static ShipDesign Empty(string name) => new(name, string.Empty, 1, System.Array.Empty<ShipPartPlacement>());

        public ShipDesign WithParts(IEnumerable<ShipPartPlacement> parts, int nextPartInstanceId) =>
            new(Name, Author, nextPartInstanceId, parts, Extensions, Frame);

        public ShipDesign WithFrame(ShipFrame frame, int nextId) => new(Name, Author, nextId, Parts, Extensions, frame);

        public ShipDesign WithName(string name) => new(name, Author, NextPartInstanceId, Parts, Extensions, Frame);

        public bool TryGetPart(int instanceId, out ShipPartPlacement placement)
        {
            foreach (var part in Parts)
            {
                if (part.InstanceId != instanceId) continue;
                placement = part;
                return true;
            }

            placement = null!;
            return false;
        }

        public override string ToString() => Frame.IsEmpty ? $"{Name} ({Parts.Count} parts)" : $"{Name} ({Parts.Count} parts, {Frame})";
    }
}
