#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship.Parts;
using TinCan.DevTools.Editor;
using TinCan.Features.ShipDesigns;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor.Tests
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
            ShipFrameDesigns.CorsairName => ShipFrameDesigns.CorsairDesign(),
            ShipFrameDesigns.ClipperName => ShipFrameDesigns.ClipperDesign(),
            _ => ShipFrameDesigns.RaftDesign(),
        };

        [TestCase(ShipFrameDesigns.StarterName)]
        [TestCase(ShipFrameDesigns.GalleonName)]
        [TestCase(ShipFrameDesigns.RaftName)]
        [TestCase(ShipFrameDesigns.CorsairName)]
        [TestCase(ShipFrameDesigns.ClipperName)]
        public void TheConfig_ListsIt_AndItLoads_AsAValidShip_ThatFlies(string name)
        {
            var config = Config();
            var file = config.BuiltInDesigns.Single(t => t != null && t.name == name + ".ship");

            var result = new ShipDesignJsonCodec(config.Limits).Decode(file.text);
            Assert.That(result.Succeeded, Is.True, result.Error);

            var catalog = InstalledParts();
            Assert.That(catalog.Problems, Is.Empty);
            var validation = new ShipDesignValidator().Validate(result.Design!, catalog, config.Limits, config.Frames);
            Assert.That(validation.Problems, Is.Empty, validation.ToString());

            var stats = new ShipStatsProcessor().Compute(result.Design!, catalog, config.Flight, config.Frames);
            Assert.That(stats.Handling, Is.Not.Null, "its core names how it flies: " + stats);
        }

        [TestCase(ShipFrameDesigns.StarterName)]
        [TestCase(ShipFrameDesigns.GalleonName)]
        [TestCase(ShipFrameDesigns.RaftName)]
        [TestCase(ShipFrameDesigns.CorsairName)]
        [TestCase(ShipFrameDesigns.ClipperName)]
        public void TheCommittedFile_IsWhatTheBuilderWrites(string name)
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(ShipDesignsAssetBuilder.DesignsFolder + name + ".ship.json");

            Assert.That(file.text.Replace("\r\n", "\n"), Is.EqualTo(new ShipDesignJsonCodec(ShipDesignLimits.Default).Encode(Built(name))));
        }

        [TestCase(ShipFrameDesigns.StarterName)]
        [TestCase(ShipFrameDesigns.GalleonName)]
        [TestCase(ShipFrameDesigns.RaftName)]
        [TestCase(ShipFrameDesigns.CorsairName)]
        [TestCase(ShipFrameDesigns.ClipperName)]
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
            Assert.That(At(mounts.QuarterdeckPortAft).y, Is.EqualTo(2.5f).Within(1e-4f), "the net rack's pole stands under open sky");

            // A cannon's seat is 1.8 m behind it (CannonAssetBuilder); the quarterdeck's stern rail is at z -8.5 m.
            foreach (int stern in new[] { mounts.SternPort, mounts.SternStarboard })
            {
                Assert.That(At(stern).z - 1.8f, Is.GreaterThan(-8.5f + 0.5f), "a stern cannon's seat stands on the quarterdeck");
            }

            // The wide fuel fittings in the aft hold clear its walls (7 m wide at the floor).
            Assert.That(Mathf.Abs(At(mounts.HoldPortAft).x), Is.LessThanOrEqualTo(2.5f));
            Assert.That(Mathf.Abs(At(mounts.HoldStarboardAft).x), Is.LessThanOrEqualTo(2.5f));
        }

        [Test]
        public void TheGalleonsSpiritHearth_IsInTheCabin_RightUnderTheHelm()
        {
            var galleon = ShipFrameDesigns.GalleonDesign();
            var shape = ShipFrameShape.From(galleon.Frame);

            Vector3 Where(string partId)
            {
                var part = galleon.Parts.Single(p => p.PartId == partId);
                Assert.That(ShipPartPose.TryGetAnchorPoint(part.Anchor, shape, out var point), Is.True);
                return point;
            }

            var hearth = Where(ShipDesignsAssetBuilder.Cairn);
            var helm = Where(ShipDesignsAssetBuilder.CoreHelm);
            Assert.That(hearth.y, Is.EqualTo(-0.5f).Within(1e-4f), "on the main deck's cabin floor, under the quarterdeck");
            Assert.That(new Vector2(hearth.x, hearth.z), Is.EqualTo(new Vector2(helm.x, helm.z)), "right under the helm");
        }

        [Test]
        public void TheCorsair_HasFourStories_JoinedByStairs_AndThreeRotorMastsOnItsCentreline()
        {
            var corsair = ShipFrameDesigns.CorsairDesign();
            var shape = ShipFrameShape.From(corsair.Frame);

            Assert.That(Enumerable.Range(0, 4).Select(s => ShipFrameUnits.ToMetres(corsair.Frame.StoryFloor(s))), Is.EqualTo(new[] { -3.5f, -0.5f, 2.5f, 5.5f }));
            var flights = shape.Stairs(ShipFrameTuning.Default.StairSlope).ToList();
            Assert.That(flights.Select(f => f.Story), Is.EquivalentTo(new[] { 1, 2, 2, 3 }), "the hatch to the hold, two up to the quarterdeck, one up the castle");

            var rotors = corsair.Parts.Where(p => p.PartId == ShipDesignsAssetBuilder.RotorMast).ToList();
            Assert.That(rotors, Has.Count.EqualTo(3));
            foreach (var rotor in rotors)
            {
                Assert.That(ShipPartPose.TryGetAnchorPoint(rotor.Anchor, shape, out var point), Is.True);
                Assert.That(point.x, Is.EqualTo(0f).Within(1e-4f), $"{rotor} stands on the centreline");
            }
        }

        [Test]
        public void TheClipper_HasThreeSailMasts_EachWithANetFromEitherRail_AndWingsUnderTheHull()
        {
            var clipper = ShipFrameDesigns.ClipperDesign();
            var shape = ShipFrameShape.From(clipper.Frame);

            Assert.That(Enumerable.Range(0, 3).Select(s => ShipFrameUnits.ToMetres(clipper.Frame.StoryFloor(s))), Is.EqualTo(new[] { -3.5f, -0.5f, 2.5f }));
            var flights = shape.Stairs(ShipFrameTuning.Default.StairSlope).ToList();
            Assert.That(flights.Select(f => f.Story), Is.EquivalentTo(new[] { 1, 2, 2 }), "the hatch to the hold, two up to the quarterdeck");

            Vector3 At(ShipPartPlacement part)
            {
                Assert.That(ShipPartPose.TryGetAnchorPoint(part.Anchor, shape, out var point), Is.True, part.ToString());
                return point;
            }

            var masts = clipper.Parts.Where(p => p.PartId == ShipDesignsAssetBuilder.SailMast).Select(At).ToList();
            var nets = clipper.Parts.Where(p => p.PartId == ShipDesignsAssetBuilder.RiggingNet).ToList();
            Assert.That(masts, Has.Count.EqualTo(3));
            Assert.That(nets, Has.Count.EqualTo(6));
            foreach (var mast in masts)
            {
                Assert.That(mast.x, Is.EqualTo(0f).Within(1e-4f), "a mast stands on the centreline");
                // The net's model reaches 4.4 m in from its rail node (sail_parts.py): the rail is 5 m out, level with the mast.
                foreach (float side in new[] { 1f, -1f })
                {
                    var net = nets.Single(p => Vector3.Distance(At(p), mast + new Vector3(5f * side, 0f, 0f)) < 1e-3f);
                    Assert.That(net.Orientation, Is.EqualTo(side > 0f ? 0 : 2), "a net leans inboard");
                }
            }

            var wings = clipper.Parts.Where(p => p.PartId == ShipDesignsAssetBuilder.WingSail).ToList();
            Assert.That(wings, Has.Count.EqualTo(4));
            foreach (var wing in wings)
            {
                var point = At(wing);
                Assert.That(point.y, Is.LessThan(-3f), "a wing hangs from the chine, low on the hull");
                Assert.That(wing.Orientation, Is.EqualTo(point.x > 0f ? 0 : 2), "a wing splays outboard");
            }

            var figurehead = At(clipper.Parts.Single(p => p.PartId == ShipDesignsAssetBuilder.Figurehead));
            Assert.That(figurehead.z, Is.EqualTo(clipper.Parts.Max(p => At(p).z)), "the figurehead is the bow's foremost part");
        }

        [Test]
        public void TheClippersMountPlates_AreWhereTheLoadoutSays_AndItsHearthIsUnderTheHelm()
        {
            var clipper = ShipFrameDesigns.ClipperDesign();
            var mounts = ShipFrameDesigns.ClipperMounts();
            var shape = ShipFrameShape.From(clipper.Frame);

            Vector3 At(int id)
            {
                Assert.That(clipper.TryGetPart(id, out var part), Is.True);
                Assert.That(part.PartId, Is.EqualTo(ShipDesignsAssetBuilder.HullMount));
                Assert.That(ShipPartPose.TryGetAnchorPoint(part.Anchor, shape, out var point), Is.True);
                return point;
            }

            foreach (int hold in new[] { mounts.HoldPortAft, mounts.HoldStarboardAft, mounts.HoldPortFore, mounts.HoldStarboardFore })
                Assert.That(At(hold).y, Is.EqualTo(-3.5f).Within(1e-4f), "in the hold");
            foreach (int deck in new[] { mounts.WaistPort, mounts.WaistStarboard, mounts.CabinFloor })
                Assert.That(At(deck).y, Is.EqualTo(-0.5f).Within(1e-4f), "on the main deck");
            foreach (int upper in new[] { mounts.QuarterdeckPort, mounts.QuarterdeckStarboard, mounts.SternPort, mounts.SternStarboard })
                Assert.That(At(upper).y, Is.EqualTo(2.5f).Within(1e-4f), "on the quarterdeck");

            // A cannon's seat is 1.8 m behind it (CannonAssetBuilder): facing out over the side, it stays on the deck.
            foreach (int cannon in new[] { mounts.QuarterdeckPort, mounts.QuarterdeckStarboard, mounts.WaistPort, mounts.WaistStarboard })
                Assert.That(Mathf.Abs(At(cannon).x) - 1.8f, Is.GreaterThan(1.5f), "a cannon's seat clears the middle of the deck");

            var hearth = clipper.Parts.Single(p => p.PartId == ShipDesignsAssetBuilder.Cairn);
            var helm = clipper.Parts.Single(p => p.PartId == ShipDesignsAssetBuilder.CoreHelm);
            Assert.That(ShipPartPose.TryGetAnchorPoint(hearth.Anchor, shape, out var hearthPoint), Is.True);
            Assert.That(ShipPartPose.TryGetAnchorPoint(helm.Anchor, shape, out var helmPoint), Is.True);
            Assert.That(new Vector2(hearthPoint.x, hearthPoint.z), Is.EqualTo(new Vector2(helmPoint.x, helmPoint.z)), "right under the helm");
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
                Is.EquivalentTo(new[]
                {
                    ShipDesignsAssetBuilder.Mast, ShipDesignsAssetBuilder.Propeller, ShipDesignsAssetBuilder.RotorMast, ShipDesignsAssetBuilder.SailMast,
                    ShipDesignsAssetBuilder.RiggingNet, ShipDesignsAssetBuilder.WingSail, ShipDesignsAssetBuilder.Figurehead,
                }));
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
