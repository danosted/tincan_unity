#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// A small part set for ship design tests: a one-cell block, a three-cell beam along +X (both with a visual), and a
    /// two-cell-tall core helm (a networked prefab, pivot at its base). Dispose destroys the definitions and prefabs.
    /// </summary>
    public sealed class ShipDesignTestParts : IDisposable
    {
        public const string Block = "hull.block";
        public const string Beam = "hull.beam";
        public const string Helm = "core.helm";

        private readonly List<ShipPartDefinition> _parts;

        public ShipDesignTestParts()
        {
            BlockVisual = new GameObject("BlockVisual");
            BeamVisual = new GameObject("BeamVisual");
            HelmPrefab = new GameObject("HelmPrefab");
            _parts = new List<ShipPartDefinition>
            {
                ShipPartDefinition.Create(Block, false, BlockVisual, null, Vector3.zero, new ShipPartStats(1f, hull: 5f)),
                ShipPartDefinition.Create(Beam, false, BeamVisual, null, Vector3.zero, new ShipPartStats(3f, hull: 10f),
                    new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(2, 0, 0)),
                ShipPartDefinition.Create(Helm, true, null, HelmPrefab, new Vector3(0f, -0.5f, 0f), new ShipPartStats(10f, lift: 100f, thrust: 100f, hull: 50f),
                    new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0)),
            };
            Catalog = new ShipPartCatalog(_parts);
        }

        public ShipPartCatalog Catalog { get; }
        public GameObject BlockVisual { get; }
        public GameObject BeamVisual { get; }
        public GameObject HelmPrefab { get; }

        public ShipPartDefinition this[string partId] => _parts.First(p => p.PartId == partId);

        /// <summary>A design from (part id, x, y, z, orientation) tuples, ids 1, 2, 3, ... in order.</summary>
        public static ShipDesign Design(params (string Part, int X, int Y, int Z, byte Rot)[] parts) =>
            new("Test", "Tests", parts.Length + 1,
                parts.Select((p, i) => new ShipPartPlacement(i + 1, p.Part, new ShipGridCell(p.X, p.Y, p.Z), p.Rot)));

        public void Dispose()
        {
            foreach (var part in _parts) Object.DestroyImmediate(part);
            Object.DestroyImmediate(BlockVisual);
            Object.DestroyImmediate(BeamVisual);
            Object.DestroyImmediate(HelmPrefab);
        }
    }
}
