#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship.Parts;
using UnityEngine;
using VContainer;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Every part contributed by a loaded installer through <c>FeatureInstaller.IExtension&lt;ShipPartDefinition&gt;</c>.
    /// A part with a malformed id is left out; of two parts with one id the first (in installer order) wins. Both are
    /// reported in <see cref="Problems"/> and logged.
    /// </summary>
    public sealed class ShipPartCatalog : IShipPartCatalog
    {
        private readonly Dictionary<string, ShipPartDefinition> _byId = new();
        private readonly List<ShipPartDefinition> _parts = new();
        private readonly List<string> _problems = new();

        [Inject]
        public ShipPartCatalog(FeatureInstallerCatalog installers)
            : this(installers.Installers
                .OfType<FeatureInstaller.IExtension<ShipPartDefinition>>()
                .SelectMany(e => e.Contributions))
        {
            foreach (var problem in _problems) Debug.LogError($"[ShipPartCatalog] {problem}");
        }

        public ShipPartCatalog(IEnumerable<ShipPartDefinition?> parts)
        {
            foreach (var part in parts)
            {
                if (part == null) continue;

                if (!ShipPartDefinition.IsValidPartId(part.PartId))
                {
                    _problems.Add($"Part {part.name} has the id \"{part.PartId}\"; ids are lower-case words separated by dots.");
                    continue;
                }

                if (_byId.TryGetValue(part.PartId, out var first))
                {
                    if (first != part) _problems.Add($"Parts {first.name} and {part.name} share the id \"{part.PartId}\"; {first.name} is used.");
                    continue;
                }

                _byId.Add(part.PartId, part);
                _parts.Add(part);
            }
        }

        public IReadOnlyList<ShipPartDefinition> Parts => _parts;

        public IReadOnlyList<string> Problems => _problems;

        public bool TryGet(string partId, out ShipPartDefinition part) => _byId.TryGetValue(partId, out part!);
    }
}
