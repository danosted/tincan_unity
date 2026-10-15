#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.ShipSockets;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The loadout file (<see cref="ShipFittingLoadoutJsonCodec"/>) and <see cref="FileShipFittingLoadoutStore"/>, in a
    /// temporary folder. Plan: .docs/plans/ship-fitting-loadouts.md.
    /// </summary>
    public class ShipFittingLoadoutTests
    {
        private const string Cannon = "weapon.cannon";
        private const string Rack = "storage.tool_rack";

        private string _directory = null!;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "TinCanLoadouts_" + System.Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private static ShipFittingLoadout Loadout(params (int Part, int Socket, string Fitting)[] fittings) =>
            new(fittings.Select(f => new ShipFittingPlacement(new ShipSocketId(f.Part, f.Socket), f.Fitting)));

        private FileShipFittingLoadoutStore Store() =>
            new(_directory, new Dictionary<string, string> { ["Galleon"] = ShipFittingLoadoutJsonCodec.Encode(Loadout((254, 0, Cannon))) });

        [Test]
        public void ALoadout_EncodesInSocketOrder_OneLinePerFitting_AndDecodesBack()
        {
            var text = ShipFittingLoadoutJsonCodec.Encode(Loadout((323, 0, Cannon), (70, 0, Rack), (254, 0, Cannon)));

            Assert.That(text, Is.EqualTo(
                "{\n  \"formatVersion\": 1,\n  \"fittings\": [\n" +
                "    {\"part\":70,\"socket\":0,\"fitting\":\"storage.tool_rack\"},\n" +
                "    {\"part\":254,\"socket\":0,\"fitting\":\"weapon.cannon\"},\n" +
                "    {\"part\":323,\"socket\":0,\"fitting\":\"weapon.cannon\"}\n" +
                "  ]\n}\n"));
            Assert.That(ShipFittingLoadoutJsonCodec.TryDecode(text, out var decoded, out var error), Is.True, error);
            Assert.That(decoded.Fittings.Select(f => (f.Socket.PartInstanceId, f.FittingId)),
                Is.EqualTo(new[] { (70, Rack), (254, Cannon), (323, Cannon) }));
        }

        [TestCase("{\"formatVersion\":2,\"fittings\":[]}", "newer game")]
        [TestCase("{\"formatVersion\":1}", "no \"fittings\"")]
        [TestCase("{\"formatVersion\":1,\"fittings\":[{\"part\":1,\"socket\":0}]}", "no \"fitting\"")]
        [TestCase("{\"formatVersion\":1,\"fittings\":[{\"part\":1,\"socket\":0,\"fitting\":\"a\"},{\"part\":1,\"socket\":0,\"fitting\":\"b\"}]}", "already has")]
        [TestCase("{\"formatVersion\":1,\"fittings\":[{\"part\":\"x\",\"socket\":0,\"fitting\":\"a\"}]}", "not a whole number")]
        [TestCase("not json", "not readable")]
        public void ABrokenFile_IsRefused_WithTheReason(string text, string reason)
        {
            Assert.That(ShipFittingLoadoutJsonCodec.TryDecode(text, out var loadout, out var error), Is.False);
            Assert.That(error, Does.Contain(reason));
            Assert.That(loadout.Fittings, Is.Empty);
        }

        [Test]
        public void ADesignWithoutALoadoutFile_HasAnEmptyLoadout()
        {
            Assert.That(Store().TryLoad("My Ship", out var loadout, out var error), Is.True, error);
            Assert.That(loadout.Fittings, Is.Empty);
        }

        [Test]
        public void ASavedLoadout_LoadsBack_BesideTheDesignFile()
        {
            var store = Store();

            Assert.That(store.TrySave("My Ship", Loadout((5, 0, Cannon), (5, 1, Rack)), out var error), Is.True, error);
            Assert.That(File.Exists(Path.Combine(_directory, "My Ship.fittings.json")), Is.True);
            Assert.That(store.TryLoad("My Ship", out var loaded, out error), Is.True, error);
            Assert.That(loaded.Fittings.Select(f => f.ToString()), Is.EqualTo(new[] { "weapon.cannon in socket 0 of part #5", "storage.tool_rack in socket 1 of part #5" }));
        }

        [Test]
        public void ABuiltInLoadout_Loads_AndCannotBeOverwritten()
        {
            var store = Store();

            Assert.That(store.TryLoad("galleon", out var loadout, out var error), Is.True, error);
            Assert.That(loadout.Fittings.Single().Socket, Is.EqualTo(new ShipSocketId(254, 0)));
            Assert.That(store.TrySave("Galleon", ShipFittingLoadout.Empty, out error), Is.False);
            Assert.That(error, Does.Contain("built-in"));
        }

        [Test]
        public void AnUnreadableFile_IsAnError_NotAnEmptyLoadout()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "Wreck.fittings.json"), "{");

            Assert.That(Store().TryLoad("Wreck", out _, out var error), Is.False);
            Assert.That(error, Does.StartWith("Wreck.fittings.json"));
        }

        [Test]
        public void WhatIsMounted_BecomesALoadout()
        {
            var loadout = ShipFittingLoadout.From(new[] { new MountedFitting(new ShipSocketId(9, 0), Cannon, 42) });

            Assert.That(loadout.Fittings.Single().ToString(), Is.EqualTo("weapon.cannon in socket 0 of part #9"));
        }
    }
}
