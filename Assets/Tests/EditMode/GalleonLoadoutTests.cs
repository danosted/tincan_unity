#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship.Sockets;
using TinCan.DevTools.Editor;
using TinCan.Features.ShipDesigns;
using TinCan.Features.ShipSockets;
using UnityEditor;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The Galleon airship (.docs/plans/ship-fitting-loadouts.md): its committed loadout is what the builder writes, the
    /// sockets config lists it, every fitting sits on a mount plate of the committed design and is a fitting the game
    /// offers, and Galleon_Prefab flies the Galleon.
    /// </summary>
    public class GalleonLoadoutTests
    {
        [Test]
        public void TheCommittedLoadout_IsWhatTheBuilderWrites_AndTheConfigListsIt()
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(ShipSocketsAssetBuilder.GalleonLoadoutPath);
            Assert.That(file, Is.Not.Null, ShipSocketsAssetBuilder.GalleonLoadoutPath);

            Assert.That(file.text.Replace("\r\n", "\n"), Is.EqualTo(ShipFittingLoadoutJsonCodec.Encode(ShipSocketsAssetBuilder.GalleonLoadout())));
            var config = AssetDatabase.LoadAssetAtPath<ShipSocketsConfig>("Assets/Settings/ShipSockets/ShipSocketsConfig.asset");
            Assert.That(config.BuiltInLoadouts, Does.Contain(file));
        }

        [Test]
        public void EveryFitting_SitsOnAMountPlate_AndIsOffered()
        {
            var galleon = ShipFrameDesigns.GalleonDesign();
            var offered = AssetDatabase.FindAssets("t:ShipFittingDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ShipFittingDefinition>(AssetDatabase.GUIDToAssetPath(guid)).FittingId)
                .ToHashSet();

            foreach (var fitting in ShipSocketsAssetBuilder.GalleonLoadout().Fittings)
            {
                Assert.That(galleon.TryGetPart(fitting.Socket.PartInstanceId, out var part), Is.True, fitting.ToString());
                Assert.That((part.PartId, fitting.Socket.Index), Is.EqualTo(("hull.mount", 0)), fitting.ToString());
                Assert.That(offered, Does.Contain(fitting.FittingId), fitting.ToString());
            }
        }

        [Test]
        public void GalleonPrefab_FliesTheGalleon_AndKeepsTheOldHullsFixturesOff()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipSocketsAssetBuilder.GalleonPrefabPath);
            var preset = prefab.GetComponent<ShipDesignPreset>();

            Assert.That(preset.DesignKey, Is.EqualTo(ShipSocketsAssetBuilder.GalleonDesignKey));
            Assert.That(preset.KeepsOff(AssetDatabase.LoadAssetAtPath<ShipFixtureDefinition>("Assets/Settings/Fixtures/FuelSystemFixture.asset")), Is.True);
            Assert.That(preset.KeepsOff(AssetDatabase.LoadAssetAtPath<ShipFixtureDefinition>("Assets/Settings/Fixtures/ShipSocketsStateFixture.asset")), Is.False);
        }
    }
}
