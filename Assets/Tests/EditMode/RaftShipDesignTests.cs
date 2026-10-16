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
    /// The built-in raft, the first ship with a frame (.docs/plans/ship-frames.md): the config lists it with the frame
    /// materials and limits, it loads as a valid ship with the installed parts, the committed file is what the asset
    /// builder writes, and it flies.
    /// </summary>
    public class RaftShipDesignTests
    {
        private static ShipPartCatalog InstalledParts() =>
            new(AssetDatabase.FindAssets("t:FeatureInstaller", new[] { "Assets/Resources/Installers" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FeatureInstaller>(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<FeatureInstaller.IExtension<ShipPartDefinition>>()
                .SelectMany(e => e.Contributions));

        private static ShipDesignsConfig Config() => AssetDatabase.LoadAssetAtPath<ShipDesignsConfig>(ShipDesignsAssetBuilder.ConfigPath);

        [Test]
        public void TheConfig_ListsTheRaft_WithFrameMaterialsAndLimits()
        {
            var config = Config();

            Assert.That(config.BuiltInDesigns.Any(t => t != null && t.name == "Raft.ship"), Is.True);
            Assert.That(config.FramePlanks, Is.Not.Null);
            Assert.That(config.FrameBeams, Is.Not.Null);
            Assert.That(config.Limits.MaxFrameElements, Is.EqualTo(ShipDesignLimits.Default.MaxFrameElements));
            Assert.That(config.Limits.MaxStories, Is.EqualTo(ShipDesignLimits.Default.MaxStories));
            Assert.That(config.Limits.MaxFaceNodes, Is.EqualTo(ShipDesignLimits.Default.MaxFaceNodes));
            Assert.That(config.Frames.MaxDrift, Is.GreaterThan(0f));
        }

        [Test]
        public void TheRaft_Loads_AsAValidShip()
        {
            var config = Config();
            var raft = config.BuiltInDesigns.Single(t => t != null && t.name == "Raft.ship");

            var result = new ShipDesignJsonCodec(config.Limits).Decode(raft.text);
            Assert.That(result.Succeeded, Is.True, result.Error);

            var validation = new ShipDesignValidator().Validate(result.Design!, InstalledParts(), config.Limits, config.Frames);
            Assert.That(validation.Problems, Is.Empty, validation.ToString());
        }

        [Test]
        public void TheCommittedRaft_IsWhatTheBuilderWrites()
        {
            var raft = AssetDatabase.LoadAssetAtPath<TextAsset>(ShipDesignsAssetBuilder.RaftPath);

            var expected = new ShipDesignJsonCodec(ShipDesignLimits.Default).Encode(ShipFrameDesigns.RaftDesign());

            Assert.That(raft.text.Replace("\r\n", "\n"), Is.EqualTo(expected));
        }

        [Test]
        public void TheRaft_Flies()
        {
            var config = Config();
            var stats = new ShipStatsProcessor().Compute(ShipFrameDesigns.RaftDesign(), InstalledParts(), config.Flight, config.Frames);

            Assert.That(stats.Lift, Is.GreaterThanOrEqualTo(stats.Mass), stats.ToString());
            Assert.That(stats.MaxSpeed, Is.GreaterThan(8f), stats.ToString());
        }
    }
}
