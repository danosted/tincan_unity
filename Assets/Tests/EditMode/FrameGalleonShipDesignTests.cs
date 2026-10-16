#nullable enable
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
    /// The built-in frame galleon (.docs/plans/ship-frames.md, S3): listed by the config, a valid ship with the installed
    /// parts (walls, railings and headroom included), the committed file is what the builder writes, it flies, its hull
    /// leans in, and its stairs rise from the hold floor to the main deck.
    /// </summary>
    public class FrameGalleonShipDesignTests
    {
        private static ShipPartCatalog InstalledParts() =>
            new(AssetDatabase.FindAssets("t:FeatureInstaller", new[] { "Assets/Resources/Installers" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FeatureInstaller>(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<FeatureInstaller.IExtension<ShipPartDefinition>>()
                .SelectMany(e => e.Contributions));

        private static ShipDesignsConfig Config() => AssetDatabase.LoadAssetAtPath<ShipDesignsConfig>(ShipDesignsAssetBuilder.ConfigPath);

        [Test]
        public void TheConfig_ListsTheFrameGalleon_AndItLoads_AsAValidShip()
        {
            var config = Config();
            var text = config.BuiltInDesigns.Single(t => t != null && t.name == "FrameGalleon.ship");

            var result = new ShipDesignJsonCodec(config.Limits).Decode(text.text);
            Assert.That(result.Succeeded, Is.True, result.Error);

            var validation = new ShipDesignValidator().Validate(result.Design!, InstalledParts(), config.Limits, config.Frames);
            Assert.That(validation.Problems, Is.Empty, validation.ToString());
        }

        [Test]
        public void TheCommittedFrameGalleon_IsWhatTheBuilderWrites()
        {
            var committed = AssetDatabase.LoadAssetAtPath<TextAsset>(ShipDesignsAssetBuilder.FrameGalleonPath);

            var expected = new ShipDesignJsonCodec(ShipDesignLimits.Default).Encode(ShipFrameDesigns.FrameGalleonDesign());

            Assert.That(committed.text.Replace("\r\n", "\n"), Is.EqualTo(expected));
        }

        [Test]
        public void TheFrameGalleon_Flies()
        {
            var config = Config();
            var stats = new ShipStatsProcessor().Compute(ShipFrameDesigns.FrameGalleonDesign(), InstalledParts(), config.Flight, config.Frames);

            Assert.That(stats.Lift, Is.GreaterThanOrEqualTo(stats.Mass), stats.ToString());
            Assert.That(stats.MaxSpeed, Is.GreaterThan(8f), stats.ToString());
        }

        [Test]
        public void ItsHull_LeansIn_AndItsStairs_RiseFromTheHoldFloorToTheMainDeck()
        {
            var design = ShipFrameDesigns.FrameGalleonDesign();
            var shape = ShipFrameShape.From(design.Frame);

            Assert.That(shape.Faces.Count(f => f.IsWall), Is.EqualTo(9));
            Assert.That(shape.Edges.Where(e => e.Edge.Id is >= 41 and <= 49).All(e => Mathf.Abs(e.To.x) > Mathf.Abs(e.From.x) || e.To.x == 0f), Is.True,
                "every post leans out going up");

            float hold = ShipFrameUnits.ToMetres(design.Frame.StoryFloor(0));
            float deck = ShipFrameUnits.ToMetres(design.Frame.StoryFloor(1));
            Assert.That((hold, deck), Is.EqualTo((-3.5f, -0.5f)));
            foreach (var stairs in design.Parts.Where(p => p.PartId == ShipDesignsAssetBuilder.Stairs))
            {
                // The stairs' origin cell stands on the hold floor; they land 5 cells forward, 3 up: on the deck.
                Assert.That(ShipGrid.CellCentre(stairs.Cell).y - 0.5f, Is.EqualTo(hold).Within(1e-4f));
                Assert.That(ShipGrid.CellCentre(stairs.Cell).y + 2.5f, Is.EqualTo(deck).Within(1e-4f));
            }
        }
    }
}
