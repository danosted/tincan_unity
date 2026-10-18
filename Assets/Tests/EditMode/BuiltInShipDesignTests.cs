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
    /// The shipped designs (.docs/plans/frame-only-ships.md): the config lists each, each loads as a valid ship with the
    /// parts the installers contribute and flies, every plank face triangulates over its whole area, and each committed
    /// file is what the asset builder writes. The Galleon keeps the old galleon's levels, so the scenarios' spots hold.
    /// </summary>
    public class BuiltInShipDesignTests
    {
        private static ShipPartCatalog InstalledParts() =>
            new(AssetDatabase.FindAssets("t:FeatureInstaller", new[] { "Assets/Resources/Installers" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FeatureInstaller>(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<FeatureInstaller.IExtension<ShipPartDefinition>>()
                .SelectMany(e => e.Contributions));

        private static ShipDesignsConfig Config() =>
            AssetDatabase.LoadAssetAtPath<ShipDesignsConfig>(ShipDesignsAssetBuilder.ConfigPath)
            ?? throw new AssertionException("run TinCan > Dev > Ship Designs > Build Assets");

        private static ShipDesign Built(string name) => name switch
        {
            ShipFrameDesigns.StarterName => ShipFrameDesigns.StarterDesign(),
            ShipFrameDesigns.GalleonName => ShipFrameDesigns.GalleonDesign(),
            _ => ShipFrameDesigns.RaftDesign(),
        };

        [TestCase(ShipFrameDesigns.StarterName)]
        [TestCase(ShipFrameDesigns.GalleonName)]
        [TestCase(ShipFrameDesigns.RaftName)]
        public void TheConfig_ListsIt_AndItLoads_AsAValidShip_ThatFlies(string name)
        {
            var config = Config();
            var file = config.BuiltInDesigns.Single(t => t != null && t.name == name + ".ship");

            var result = new ShipDesignJsonCodec(config.Limits).Decode(file.text);
            Assert.That(result.Succeeded, Is.True, result.Error);

            var catalog = InstalledParts();
            Assert.That(catalog.Problems, Is.Empty);
            var validation = new ShipDesignValidator().Validate(result.Design!, catalog, config.Limits, config.Frames, config.Flight);
            Assert.That(validation.Problems, Is.Empty, validation.ToString());

            var stats = new ShipStatsProcessor().Compute(result.Design!, catalog, config.Flight, config.Frames);
            Assert.That(stats.MaxSpeed, Is.GreaterThan(8f), stats.ToString());
        }

        [TestCase(ShipFrameDesigns.StarterName)]
        [TestCase(ShipFrameDesigns.GalleonName)]
        [TestCase(ShipFrameDesigns.RaftName)]
        public void TheCommittedFile_IsWhatTheBuilderWrites(string name)
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(ShipDesignsAssetBuilder.DesignsFolder + name + ".ship.json");

            Assert.That(file.text.Replace("\r\n", "\n"), Is.EqualTo(new ShipDesignJsonCodec(ShipDesignLimits.Default).Encode(Built(name))));
        }

        [TestCase(ShipFrameDesigns.StarterName)]
        [TestCase(ShipFrameDesigns.GalleonName)]
        [TestCase(ShipFrameDesigns.RaftName)]
        public void EveryPlankFace_IsCoveredByItsTriangles(string name)
        {
            foreach (var face in ShipFrameShape.From(Built(name).Frame).PlankFaces)
            {
                var normal = Newell(face).normalized;
                float triangles = face.EachTriangle().Sum(t => Mathf.Abs(Vector3.Dot(Vector3.Cross(t.B - t.A, t.C - t.A), normal)) * 0.5f);
                Assert.That(triangles, Is.EqualTo(Newell(face).magnitude * 0.5f).Within(0.01f), $"{face.Face}: a corner left out of the triangles");
            }
        }

        [Test]
        public void TheGalleon_KeepsTheOldLevels_AndItsStairsJoinThem()
        {
            var galleon = ShipFrameDesigns.GalleonDesign();
            var frame = galleon.Frame;

            Assert.That(Enumerable.Range(0, 3).Select(s => ShipFrameUnits.ToMetres(frame.StoryFloor(s))), Is.EqualTo(new[] { -3.5f, -0.5f, 2.5f }));
            var flights = ShipFrameShape.From(frame).Stairs(ShipFrameTuning.Default.StairSlope).ToList();
            Assert.That(flights.Select(f => f.Story), Is.EquivalentTo(new[] { 1, 2, 2 }), "the hatch to the hold, two up to the quarterdeck");
            Assert.That(flights.Select(f => f.Rise), Is.All.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void TheGalleonsMountPlates_AreWhereTheLoadoutSays()
        {
            var galleon = ShipFrameDesigns.GalleonDesign();
            var mounts = ShipFrameDesigns.GalleonMounts();
            var shape = ShipFrameShape.From(galleon.Frame);

            Vector3 At(int id)
            {
                Assert.That(galleon.TryGetPart(id, out var part), Is.True);
                Assert.That(part.PartId, Is.EqualTo(ShipDesignsAssetBuilder.HullMount));
                Assert.That(ShipPartPose.TryGetAnchorPoint(part.Anchor, shape, out var point), Is.True);
                return point;
            }

            Assert.That(At(mounts.HoldStarboardFore).y, Is.EqualTo(-3.5f).Within(1e-4f), "the chart room is in the hold");
            Assert.That(At(mounts.BowPort).y, Is.EqualTo(-0.5f).Within(1e-4f));
            Assert.That(At(mounts.SternStarboard).y, Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(At(mounts.Balcony).x, Is.GreaterThan(4.5f), "the balcony is outboard of the hull");
        }

        [Test]
        public void TheShipyardTestProfile_LoadsShipDesignsAndTheShipyard()
        {
            var profile = AssetDatabase.LoadAssetAtPath<FeatureProfile>(ShipDesignsAssetBuilder.ProfilePath);
            var installers = profile.ResolveInstallers().Select(i => i.GetType().Name).ToList();

            Assert.That(installers, Does.Contain("ShipDesignsFeatureInstaller").And.Contain("ShipyardFeatureInstaller")
                .And.Contain("HelmFeatureInstaller").And.Contain("StationsFeatureInstaller"),
                "both asset builders add to this profile; neither may drop the other's installer");
        }

        [Test]
        public void EveryInstalledPart_HasSomethingToBuild_AndOneIsTheCairn()
        {
            var parts = InstalledParts().Parts;
            foreach (var part in parts)
            {
                Assert.That(part.Visual != null || part.NetworkedPrefab != null, Is.True, $"{part.PartId} builds nothing");
            }

            Assert.That(parts.Count(p => p.IsCairn), Is.EqualTo(1));
            Assert.That(parts.Where(p => p.Attach == ShipPartAttach.Node).Select(p => p.PartId),
                Is.EquivalentTo(new[] { ShipDesignsAssetBuilder.Mast, ShipDesignsAssetBuilder.Propeller }));
        }

        /// <summary>A face loop's Newell vector: along its normal, as long as twice its area seen along that normal.</summary>
        private static Vector3 Newell(ShipFrameShape.FaceShape face)
        {
            var sum = Vector3.zero;
            for (int i = 0; i < face.Corners.Count; i++) sum += Vector3.Cross(face.Corners[i], face.Corners[(i + 1) % face.Corners.Count]);
            return sum;
        }
    }
}
