#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A ship design: the one model the shipyard edits, files store, the network carries and the game builds a ship from.
    /// Immutable; edits make a new design (<see cref="ShipDesignEditProcessor"/>). Parts are kept in
    /// <see cref="ShipPartPlacement.InstanceId"/> order, so a design always encodes to the same bytes.
    /// Plan: .docs/plans/modular-airship-builder.md.
    /// </summary>
    public sealed class ShipDesign
    {
        private static readonly IReadOnlyDictionary<string, string> NoExtensions = new Dictionary<string, string>();

        public ShipDesign(string name, string author, int nextPartInstanceId, IEnumerable<ShipPartPlacement> parts,
            IReadOnlyDictionary<string, string>? extensions = null)
        {
            Name = name;
            Author = author;
            Parts = parts.OrderBy(p => p.InstanceId).ToList();
            NextPartInstanceId = nextPartInstanceId;
            Extensions = extensions ?? NoExtensions;
        }

        public string Name { get; }
        public string Author { get; }

        /// <summary>The id the next placed part gets. Ids are never reused, even after a part is removed.</summary>
        public int NextPartInstanceId { get; }

        public IReadOnlyList<ShipPartPlacement> Parts { get; }

        /// <summary>Top-level fields this build does not know, as raw JSON (see <see cref="ShipPartPlacement.Extensions"/>).</summary>
        public IReadOnlyDictionary<string, string> Extensions { get; }

        public static ShipDesign Empty(string name) => new(name, string.Empty, 1, System.Array.Empty<ShipPartPlacement>());

        public ShipDesign WithParts(IEnumerable<ShipPartPlacement> parts, int nextPartInstanceId) =>
            new(Name, Author, nextPartInstanceId, parts, Extensions);

        public ShipDesign WithName(string name) => new(name, Author, NextPartInstanceId, Parts, Extensions);

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

        public override string ToString() => $"{Name} ({Parts.Count} parts)";
    }
}
