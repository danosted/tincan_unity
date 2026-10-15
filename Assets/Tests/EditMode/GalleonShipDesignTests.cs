#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship.Parts;
using TinCan.DevTools.Editor;
using TinCan.Features.ShipDesigns;
using UnityEditor;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The built-in galleon (.docs/plans/airship-parts-pack.md): it loads as a valid ship with the installed parts, the
    /// committed file is what the asset builder writes, it flies, and its levels are joined: every staircase starts on a
    /// floor and ends at a deck, and there is a player's headroom over every deck.
    /// </summary>
    public class GalleonShipDesignTests
    {
        private const float PlayerHeight = 2f;

        private static ShipPartCatalog InstalledParts() =>
            new(AssetDatabase.FindAssets("t:FeatureInstaller", new[] { "Assets/Resources/Installers" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FeatureInstaller>(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<FeatureInstaller.IExtension<ShipPartDefinition>>()
                .SelectMany(e => e.Contributions));

        [Test]
        public void TheConfig_ListsTheGalleon_AndItLoads_AsAValidShip()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShipDesignsConfig>(ShipDesignsAssetBuilder.ConfigPath);
            var galleon = config.BuiltInDesigns.Single(t => t != null && t.name == "Galleon.ship");

            var result = new ShipDesignJsonCodec(config.Limits).Decode(galleon.text);
            Assert.That(result.Succeeded, Is.True, result.Error);

            var validation = new ShipDesignValidator().Validate(result.Design!, InstalledParts(), config.Limits);
            Assert.That(validation.Problems, Is.Empty, validation.ToString());
        }

        [Test]
        public void TheCommittedGalleon_IsWhatTheBuilderWrites()
        {
            var galleon = AssetDatabase.LoadAssetAtPath<TextAsset>(ShipDesignsAssetBuilder.GalleonPath);

            var expected = new ShipDesignJsonCodec(ShipDesignLimits.Default).Encode(ShipDesignsAssetBuilder.GalleonDesign());

            Assert.That(galleon.text.Replace("\r\n", "\n"), Is.EqualTo(expected));
        }

        [Test]
        public void TheGalleon_Flies()
        {
            var stats = new ShipStatsProcessor().Compute(ShipDesignsAssetBuilder.GalleonDesign(), InstalledParts(), ShipStatsTuning.Default);

            Assert.That(stats.Lift, Is.GreaterThanOrEqualTo(stats.Mass), stats.ToString());
            Assert.That(stats.MaxSpeed, Is.GreaterThan(8f), stats.ToString());
        }

        [Test]
        public void EveryStaircase_StartsOnAFloor_AndEndsAtADeck()
        {
            var design = ShipDesignsAssetBuilder.GalleonDesign();
            var walkable = WalkableTops(design);
            var flights = design.Parts.Where(p => p.PartId == ShipDesignsAssetBuilder.Stairs).ToList();
            Assert.That(flights, Has.Count.EqualTo(5), "three side by side in the hatch, one up each side to the quarterdeck");

            foreach (var flight in flights)
            {
                // The stairs fill 1 x 3 x 5 cells rising along their forward axis: the floor below the first cell, the
                // deck one cell beyond the last, on the level of the top cells.
                var below = Rotated(flight, new Vector3Int(0, -1, 0));
                var landing = Rotated(flight, new Vector3Int(0, 2, 5));
                Assert.That(walkable.Contains(below), Is.True, $"stairs {flight.InstanceId} at {flight.Cell}: nothing to stand on at {below}");
                Assert.That(walkable.Contains(landing), Is.True, $"stairs {flight.InstanceId} at {flight.Cell}: no deck at {landing}");
            }
        }

        [Test]
        public void EveryDeck_LeavesHeadroomForAPlayer()
        {
            var design = ShipDesignsAssetBuilder.GalleonDesign();
            var decks = design.Parts.Where(p => p.PartId is ShipDesignsAssetBuilder.HullDeck or ShipDesignsAssetBuilder.HullMount).ToList();
            var deckCells = new HashSet<ShipGridCell>(decks.Select(p => p.Cell));
            Assert.That(decks.Select(d => d.Cell.Y).Distinct().Count(), Is.EqualTo(3), "hold, main deck and quarterdeck");

            foreach (var deck in design.Parts.Where(p => p.PartId is ShipDesignsAssetBuilder.HullDeck or ShipDesignsAssetBuilder.HullMount))
            {
                // A 0.2 m slab at the top of its cell: the next deck up must be at least 3 cells higher for a 2 m player.
                for (int up = 1; up <= Mathf.CeilToInt(PlayerHeight); up++)
                {
                    var above = new ShipGridCell(deck.Cell.X, deck.Cell.Y + up, deck.Cell.Z);
                    Assert.That(deckCells.Contains(above), Is.False, $"a deck at {above} leaves no headroom over the deck at {deck.Cell}");
                }
            }
        }

        private static HashSet<ShipGridCell> WalkableTops(ShipDesign design) =>
            new(design.Parts
                .Where(p => p.PartId is ShipDesignsAssetBuilder.HullDeck or ShipDesignsAssetBuilder.HullMount or ShipDesignsAssetBuilder.HullBlock)
                .Select(p => p.Cell)
                .Concat(design.Parts.Where(p => p.PartId == ShipDesignsAssetBuilder.Wall).Select(p => Rotated(p, new Vector3Int(0, 2, 0)))));

        private static ShipGridCell Rotated(ShipPartPlacement placement, Vector3Int offset)
        {
            var turned = ShipPartOrientation.Rotate(offset, placement.Orientation);
            return new ShipGridCell(placement.Cell.X + turned.X, placement.Cell.Y + turned.Y, placement.Cell.Z + turned.Z);
        }
    }
}
