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

            var asVersion2 = text.Replace("\"formatVersion\": 1,", "\"formatVersion\": 2,").Replace("\"nextPartInstanceId\":", "\"nextId\":");
            Assert.That(_codec.Encode(design), Is.EqualTo(asVersion2), "saved again, it is the same design in version 2's canonical form");
        }

        [Test]
        public void TheV2GoldenFile_Loads_WithItsFrame_AndIsWrittenBackByteForByte()
        {
            var text = File.ReadAllText(GoldenPath("v2-golden.ship.json")).Replace("\r\n", "\n");

            var result = _codec.Decode(text);

            Assert.That(result.Succeeded, Is.True, result.Error);
            var design = result.Design!;
            Assert.That(design.NextPartInstanceId, Is.EqualTo(ShipFrameTestDesigns.NextId));
            var frame = design.Frame;
            Assert.That(frame.Base, Is.EqualTo(-5));
            Assert.That(frame.StoryHeights, Is.EqualTo(new[] { 30 }));
            Assert.That(frame.Nodes.Select(n => (n.Id, n.X, n.Y, n.Z)), Is.EqualTo(new[] { (1, -20, 0, -20), (2, -20, 0, 20), (3, 20, 2, 20), (4, 20, 0, -20) }));
            Assert.That(frame.Edges.Select(e => (e.A, e.B, e.Railing)), Is.EqualTo(new[] { (1, 2, false), (2, 3, true), (3, 4, false), (4, 1, false) }));
            Assert.That(frame.Faces.Select(f => (f.Id, f.Fill)), Is.EqualTo(new[] { (9, ShipFrameFill.Planks) }));
            Assert.That(frame.Faces[0].Nodes, Is.EqualTo(new[] { 1, 2, 3, 4 }));

            using var parts = new ShipDesignTestParts();
            var validation = new ShipDesignValidator().Validate(design, parts.Catalog, ShipDesignLimits.Default);
            Assert.That(validation.Problems, Is.Empty, validation.ToString());

            Assert.That(_codec.Encode(design), Is.EqualTo(text), "the canonical form is the file format");
            Assert.That(_codec.Encode(ShipFrameTestDesigns.Deck()), Is.EqualTo(text), "the golden file is the test deck");
        }

        [Test]
        public void ADesignWithAFrame_RoundTrips_AndOneWithoutWritesNoFrame()
        {
            var design = ShipFrameTestDesigns.Deck();

            var back = _codec.Decode(_codec.Encode(design)).Design!;

            Assert.That(ShipDesignHash.Compute(back), Is.EqualTo(ShipDesignHash.Compute(design)));
            Assert.That(_codec.Encode(Design((Helm, 0, 0, 0, 0))), Does.Not.Contain(ShipDesignFormat.FrameField));
        }

        [TestCase("{\"formatVersion\":2,\"parts\":[],\"frame\":[]}", "not an object")]
        [TestCase("{\"formatVersion\":2,\"parts\":[],\"frame\":{\"nodes\":[{\"id\":1,\"story\":0,\"pos\":[0,0]}]}}", "three whole numbers")]
        [TestCase("{\"formatVersion\":2,\"parts\":[],\"frame\":{\"faces\":[{\"id\":1,\"nodes\":[1,2,3],\"fill\":\"glass\"}]}}", "no fill")]
        [TestCase("{\"formatVersion\":2,\"parts\":[],\"frame\":{\"edges\":[{\"id\":1,\"a\":1}]}}", "\"b\"")]
        [TestCase("{\"formatVersion\":2,\"parts\":[],\"frame\":{\"stories\":[30,30,30,30,30,30,30,30,30]}}", "stories")]
        public void BrokenFrames_AreRefused_WithAReason(string text, string reason)
        {
            var result = _codec.Decode(text);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain(reason));
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
