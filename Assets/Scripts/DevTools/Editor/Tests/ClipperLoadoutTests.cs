#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.ShipSockets;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor.Tests
{
    /// <summary>
    /// The Clipper's loadout: the committed file is what the builder writes and the sockets config lists it, every fitting
    /// sits on a mount plate of the design and is one the game offers, and with the helm there is a station for each of a
    /// crew of eight (the host's default cap).
    /// </summary>
    public class ClipperLoadoutTests
    {
        [Test]
        public void TheCommittedLoadout_IsWhatTheBuilderWrites_AndTheConfigListsIt()
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(ShipSocketsAssetBuilder.ClipperLoadoutPath);
            Assert.That(file, Is.Not.Null, ShipSocketsAssetBuilder.ClipperLoadoutPath);

            Assert.That(file.text.Replace("\r\n", "\n"), Is.EqualTo(ShipFittingLoadoutJsonCodec.Encode(ShipSocketsAssetBuilder.ClipperLoadout())));
            var config = AssetDatabase.LoadAssetAtPath<ShipSocketsConfig>("Assets/Settings/ShipSockets/ShipSocketsConfig.asset");
            Assert.That(config.BuiltInLoadouts, Does.Contain(file));
        }

        [Test]
        public void EveryFitting_SitsOnAMountPlate_AndIsOffered()
        {
            var clipper = ShipFrameDesigns.ClipperDesign();
            var offered = AssetDatabase.FindAssets("t:ShipFittingDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ShipFittingDefinition>(AssetDatabase.GUIDToAssetPath(guid)).FittingId)
                .ToHashSet();

            foreach (var fitting in ShipSocketsAssetBuilder.ClipperLoadout().Fittings)
            {
                Assert.That(clipper.TryGetPart(fitting.Socket.PartInstanceId, out var part), Is.True, fitting.ToString());
                Assert.That((part.PartId, fitting.Socket.Index), Is.EqualTo(("hull.mount", 0)), fitting.ToString());
                Assert.That(offered, Does.Contain(fitting.FittingId), fitting.ToString());
            }
        }

        [Test]
        public void WithTheHelm_ThereIsAStationForEachOfACrewOfEight()
        {
            var fittings = ShipSocketsAssetBuilder.ClipperLoadout().Fittings;

            Assert.That(fittings.Select(f => f.Socket).Distinct().Count(), Is.EqualTo(fittings.Count), "one fitting per plate");
            Assert.That(fittings.Count + 1, Is.GreaterThanOrEqualTo(HostOptions.DefaultMaxPlayers));
        }
    }
}
