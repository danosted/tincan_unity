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
    /// A small part set for ship design tests, all standing on plank floors unless said: a one-block block, a three-block
    /// beam along +X (both with a visual), a two-block-tall core helm (a networked prefab) that lifts and drives, a post
    /// that sits on a node, and the cairn. Dispose destroys the definitions and prefabs.
    /// </summary>
    public sealed class ShipDesignTestParts : IDisposable
    {
        public const string Block = "hull.block";
        public const string Beam = "hull.beam";
        public const string Helm = "core.helm";
        public const string Post = "hull.post";
        public const string Cairn = "spirit.cairn";

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
                ShipPartDefinition.Create(Helm, true, null, HelmPrefab, Vector3.zero, new ShipPartStats(10f, lift: 5000f, thrust: 100f, hull: 50f),
                    new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0)),
                ShipPartDefinition.Create(Post, false, BlockVisual, null, Vector3.zero, new ShipPartStats(1f), ShipPartAttach.Node, false),
                ShipPartDefinition.Create(Cairn, false, BlockVisual, null, Vector3.zero, new ShipPartStats(20f), ShipPartAttach.Face, true),
            };
            Catalog = new ShipPartCatalog(_parts);
        }

        public ShipPartCatalog Catalog { get; }
        public GameObject BlockVisual { get; }
        public GameObject BeamVisual { get; }
        public GameObject HelmPrefab { get; }

        public ShipPartDefinition this[string partId] => _parts.First(p => p.PartId == partId);

        /// <summary>
        /// A design of face parts on <see cref="ShipFrameTestDesigns.Floor"/> (a 20 x 20 m plank floor at -0.5 m, face 9)
        /// from (part id, x, z in whole metres, yaw) tuples; part ids 10, 11, 12, ... in order.
        /// </summary>
        public static ShipDesign Design(params (string Part, int X, int Z, byte Rot)[] parts) =>
            new("Test", "Tests", ShipFrameTestDesigns.NextId - 1 + parts.Length,
                parts.Select((p, i) => new ShipPartPlacement(ShipFrameTestDesigns.NextId - 1 + i, p.Part,
                    ShipPartAnchor.OnFace(ShipFrameTestDesigns.FloorFace, p.X * ShipFrameUnits.PerMetre, p.Z * ShipFrameUnits.PerMetre), p.Rot)),
                frame: ShipFrameTestDesigns.Floor());

        public void Dispose()
        {
            foreach (var part in _parts) Object.DestroyImmediate(part);
            Object.DestroyImmediate(BlockVisual);
            Object.DestroyImmediate(BeamVisual);
            Object.DestroyImmediate(HelmPrefab);
        }
    }
}
