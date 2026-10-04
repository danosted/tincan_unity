#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Parts;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Builds one design under one root and keeps it in step: each apply removes the parts that went or changed and builds
    /// the new ones, keyed by instance id. Used for ships in play (<see cref="ShipAssemblyUseCase"/>) and for the
    /// shipyard's preview, so what is built is what flies. A part builds its visual; with
    /// <c>buildFunctionalParts</c> a part without one builds its networked prefab instead (the preview's helm; the
    /// builder decides what to strip). Unknown parts are kept as placeholders that build nothing.
    /// </summary>
    public sealed class ShipHullAssembler
    {
        private readonly Dictionary<int, (ShipPartPlacement Placement, object? Handle)> _parts = new();
        private readonly IShipPartCatalog _catalog;
        private readonly IShipHullBuilder _builder;
        private readonly bool _buildFunctionalParts;

        public ShipHullAssembler(IShipPartCatalog catalog, IShipHullBuilder builder, bool buildFunctionalParts)
        {
            _catalog = catalog;
            _builder = builder;
            _buildFunctionalParts = buildFunctionalParts;
        }

        /// <summary>Parts with something built for them.</summary>
        public int BuiltCount => _parts.Values.Count(p => p.Handle != null);

        public int UnknownCount { get; private set; }

        /// <summary>Brings the root in step with the design; returns the known parts added by this apply.</summary>
        public IReadOnlyList<(ShipPartPlacement Placement, ShipPartDefinition Part)> Apply(ShipDesign design, Transform root)
        {
            var wanted = design.Parts.ToDictionary(p => p.InstanceId);
            foreach (var instanceId in _parts.Keys.ToList())
            {
                var (placement, handle) = _parts[instanceId];
                if (wanted.TryGetValue(instanceId, out var now) && Same(placement, now)) continue;

                if (handle != null) _builder.Remove(handle);
                _parts.Remove(instanceId);
            }

            var added = new List<(ShipPartPlacement, ShipPartDefinition)>();
            foreach (var placement in design.Parts)
            {
                if (_parts.ContainsKey(placement.InstanceId)) continue;
                if (!_catalog.TryGet(placement.PartId, out var part))
                {
                    _parts.Add(placement.InstanceId, (placement, null));
                    continue;
                }

                var prefab = part.Visual != null ? part.Visual : _buildFunctionalParts ? part.NetworkedPrefab : null;
                object? handle = prefab != null
                    ? _builder.Build(root, prefab, ShipPartPose.LocalPosition(placement, part), ShipPartPose.LocalRotation(placement))
                    : null;
                _parts.Add(placement.InstanceId, (placement, handle));
                added.Add((placement, part));
            }

            UnknownCount = design.Parts.Count(p => !_catalog.TryGet(p.PartId, out _));
            if (ShipPartPose.TryGetBounds(design, _catalog, out var bounds)) _builder.FitShipVolume(root, bounds);
            return added;
        }

        public void Clear()
        {
            foreach (var (_, handle) in _parts.Values)
            {
                if (handle != null) _builder.Remove(handle);
            }

            _parts.Clear();
            UnknownCount = 0;
        }

        private static bool Same(ShipPartPlacement a, ShipPartPlacement b) =>
            a.PartId == b.PartId && a.Cell == b.Cell && a.Orientation == b.Orientation;
    }
}
