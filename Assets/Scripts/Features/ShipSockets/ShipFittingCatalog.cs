#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship.Sockets;
using UnityEngine;
using VContainer;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Every fitting a loaded installer contributes through <c>FeatureInstaller.IExtension&lt;ShipFittingDefinition&gt;</c>.
    /// A fitting without a prefab or with a malformed id is left out; of two with one id the first wins.
    /// </summary>
    public sealed class ShipFittingCatalog : IShipFittingCatalog
    {
        private readonly Dictionary<string, ShipFittingDefinition> _byId = new();
        private readonly List<ShipFittingDefinition> _fittings = new();

        [Inject]
        public ShipFittingCatalog(FeatureInstallerCatalog installers)
            : this(installers.Installers.OfType<FeatureInstaller.IExtension<ShipFittingDefinition>>().SelectMany(e => e.Contributions))
        {
        }

        public ShipFittingCatalog(IEnumerable<ShipFittingDefinition?> fittings)
        {
            foreach (var fitting in fittings)
            {
                if (fitting == null) continue;
                if (fitting.Prefab == null || !ShipFittingDefinition.IsValidFittingId(fitting.FittingId))
                {
                    Debug.LogError($"[ShipFittingCatalog] Fitting {fitting.name} needs a prefab and an id like weapon.cannon.", fitting);
                    continue;
                }

                if (_byId.ContainsKey(fitting.FittingId)) continue;
                _byId.Add(fitting.FittingId, fitting);
                _fittings.Add(fitting);
            }
        }

        public IReadOnlyList<ShipFittingDefinition> Fittings => _fittings;

        public bool TryGet(string fittingId, out ShipFittingDefinition fitting) => _byId.TryGetValue(fittingId, out fitting!);
    }
}
