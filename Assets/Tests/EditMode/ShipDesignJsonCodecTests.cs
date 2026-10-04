#nullable enable
using System.IO;
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipDesignJsonCodec"/>: the design file format. The golden files under <c>ShipDesigns/</c> are committed
    /// samples of each format version; they must keep loading for as long as the game reads designs.
    /// </summary>
    public class ShipDesignJsonCodecTests
    {
        private static string GoldenPath(string file) => Path.Combine(Application.dataPath, "Tests/EditMode/ShipDesigns", file);

        private readonly ShipDesignJsonCodec _codec = new(ShipDesignLimits.Default);

        [Test]
        public void TheV1GoldenFile_Loads_Validates_AndIsWrittenBackByteForByte()
        {
            var text = File.ReadAllText(GoldenPath("v1-golden.ship.json")).Replace("\r\n", "\n");

            var result = _codec.Decode(text);

            Assert.That(result.Succeeded, Is.True, result.Error);
            var design = result.Design!;
            Assert.That((design.Name, design.Author, design.NextPartInstanceId), Is.EqualTo(("Golden", "Tests", 6)));
            Assert.That(design.Parts.Select(p => p.InstanceId), Is.EqualTo(new[] { 1, 2, 4, 5 }));
            var block = design.Parts[2];
            Assert.That((block.PartId, block.Cell, block.Orientation), Is.EqualTo((Block, new ShipGridCell(0, 0, -1), (byte)3)));

            using var parts = new ShipDesignTestParts();
            var validation = new ShipDesignValidator().Validate(design, parts.Catalog, ShipDesignLimits.Default);
            Assert.That(validation.IsValid && validation.Problems.Count == 0, Is.True, validation.ToString());

            Assert.That(_codec.Encode(design), Is.EqualTo(text), "the canonical form is the file format");
        }

        [Test]
        public void ADesign_RoundTrips()
        {
            var design = Design((Helm, 0, 0, 0, 0), (Beam, -3, -1, 7, 2), (Block, 100, -100, 5, 23));

            var back = _codec.Decode(_codec.Encode(design)).Design!;

            Assert.That(ShipDesignHash.Compute(back), Is.EqualTo(ShipDesignHash.Compute(design)));
            Assert.That(back.Name, Is.EqualTo("Test"));
        }

        [Test]
        public void Encoding_IsCanonical_WhateverOrderThePartsCameIn()
        {
            var parts = Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0), (Block, 2, 0, 0, 0)).Parts;
            var forward = new ShipDesign("C", "", 4, parts);
            var backward = new ShipDesign("C", "", 4, parts.Reverse());

            Assert.That(_codec.Encode(backward), Is.EqualTo(_codec.Encode(forward)));
            Assert.That(_codec.Encode(forward), Does.Not.Contain("\r"), "line ends are the same on every platform");
        }

        [Test]
        public void FieldsThisBuildDoesNotKnow_SurviveALoadAndSave()
        {
            const string fromTheFuture = "{\"formatVersion\":1,\"name\":\"F\",\"paint\":{\"hull\":\"#aa3300\",\"trim\":[1,2]}," +
                                         "\"parts\":[{\"id\":1,\"part\":\"core.helm\",\"cell\":[0,0,0],\"rot\":0,\"wiring\":[2,3],\"label\":\"Bridge\"}]}";

            var design = _codec.Decode(fromTheFuture).Design!;
            var saved = _codec.Encode(design);
            var again = _codec.Decode(saved).Design!;

            Assert.That(again.Extensions["paint"], Is.EqualTo("{\"hull\":\"#aa3300\",\"trim\":[1,2]}"));
            Assert.That(again.Parts[0].Extensions["wiring"], Is.EqualTo("[2,3]"));
            Assert.That(again.Parts[0].Extensions["label"], Is.EqualTo("\"Bridge\""));
            Assert.That(again.NextPartInstanceId, Is.EqualTo(2), "missing next id: one past the highest");
        }

        [Test]
        public void UnknownPartIds_AreKept()
        {
            var design = Design((Helm, 0, 0, 0, 0), ("hull.from_the_future", 1, 0, 0, 0));

            var back = _codec.Decode(_codec.Encode(design)).Design!;

            Assert.That(back.Parts[1].PartId, Is.EqualTo("hull.from_the_future"));
        }

        [Test]
        public void AFileFromANewerGame_IsRefused_NotHalfRead()
        {
            var result = _codec.Decode("{\"formatVersion\":99,\"parts\":[]}");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("newer"));
        }

        [TestCase("", "empty")]
        [TestCase("not json", "not readable")]
        [TestCase("[1,2]", "not readable")]
        [TestCase("{\"parts\":[]}", "formatVersion")]
        [TestCase("{\"formatVersion\":0,\"parts\":[]}", "does not exist")]
        [TestCase("{\"formatVersion\":1}", "parts")]
        [TestCase("{\"formatVersion\":1,\"parts\":[{\"id\":1,\"cell\":[0,0,0]}]}", "\"part\"")]
        [TestCase("{\"formatVersion\":1,\"parts\":[{\"id\":1,\"part\":\"a\",\"cell\":[0,0]}]}", "three whole numbers")]
        [TestCase("{\"formatVersion\":1,\"parts\":[{\"id\":1,\"part\":\"a\",\"cell\":[0,0.5,0]}]}", "whole number")]
        [TestCase("{\"formatVersion\":1,\"parts\":[{\"id\":1,\"part\":\"a\",\"cell\":[0,0,0],\"rot\":300}]}", "out of range")]
        [TestCase("{\"formatVersion\":1,\"parts\":[{\"id\":99999999999,\"part\":\"a\",\"cell\":[0,0,0]}]}", "not readable")]
        [TestCase("{\"formatVersion\":1,\"parts\":[]} {}", "not readable")]
        public void BrokenFiles_AreRefused_WithAReason(string text, string reason)
        {
            var result = _codec.Decode(text);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain(reason));
        }

        [Test]
        public void Limits_AreCheckedBeforeAnythingIsBuilt()
        {
            var limits = ShipDesignLimits.Default;
            limits.MaxParts = 1;
            limits.MaxTextLength = 1024;
            var codec = new ShipDesignJsonCodec(limits);

            Assert.That(codec.Decode(_codec.Encode(Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0)))).Error, Does.Contain("at most 1"));
            Assert.That(codec.Decode(new string(' ', 2000) + "{}").Error, Does.Contain("characters"));
        }
    }
}
