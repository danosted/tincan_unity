#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship.Parts;
using TinCan.DevTools.Editor;
using TinCan.Features.ShipDesigns;
using UnityEditor;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The shipped assets: the built-in starter design loads and is a valid ship with the parts the installers contribute,
    /// and the committed file is what the asset builder writes.
    /// </summary>
    public class StarterShipDesignTests
    {
        private static ShipPartCatalog InstalledParts() =>
            new(AssetDatabase.FindAssets("t:FeatureInstaller", new[] { "Assets/Resources/Installers" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FeatureInstaller>(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<FeatureInstaller.IExtension<ShipPartDefinition>>()
                .SelectMany(e => e.Contributions));

        [Test]
        public void TheConfig_ListsTheStarter_AndItLoads_AsAValidShip()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShipDesignsConfig>(ShipDesignsAssetBuilder.ConfigPath);
            Assert.That(config, Is.Not.Null, "run TinCan > Dev > Ship Designs > Build Assets");
            var starter = config.BuiltInDesigns.Single(t => t != null && t.name == "Starter.ship");

            var result = new ShipDesignJsonCodec(config.Limits).Decode(starter.text);
            Assert.That(result.Succeeded, Is.True, result.Error);

            var catalog = InstalledParts();
            Assert.That(catalog.Problems, Is.Empty);
            var validation = new ShipDesignValidator().Validate(result.Design!, catalog, config.Limits);
            Assert.That(validation.Problems, Is.Empty, validation.ToString());
        }

        [Test]
        public void TheCommittedStarter_IsWhatTheBuilderWrites()
        {
            var starter = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(ShipDesignsAssetBuilder.StarterPath);

            var expected = new ShipDesignJsonCodec(ShipDesignLimits.Default).Encode(ShipDesignsAssetBuilder.StarterDesign());

            Assert.That(starter.text.Replace("\r\n", "\n"), Is.EqualTo(expected));
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
        public void EveryInstalledPart_HasSomethingToBuild()
        {
            foreach (var part in InstalledParts().Parts)
            {
                Assert.That(part.Visual != null || part.NetworkedPrefab != null, Is.True, $"{part.PartId} builds nothing");
            }
        }
    }
}
