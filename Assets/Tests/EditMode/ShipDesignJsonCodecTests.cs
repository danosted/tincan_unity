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

        private static string Golden(string file) => File.ReadAllText(GoldenPath(file)).Replace("\r\n", "\n");

        private readonly ShipDesignJsonCodec _codec = new(ShipDesignLimits.Default);

        [Test]
        public void TheV1GoldenFile_Loads_WithItsGridPartsDropped_ButKept()
        {
            var result = _codec.Decode(Golden("v1-golden.ship.json"));

            Assert.That(result.Succeeded, Is.True, result.Error);
            var design = result.Design!;
            Assert.That((design.Name, design.Author, design.NextPartInstanceId), Is.EqualTo(("Golden", "Tests", 6)));
            Assert.That(design.Parts, Is.Empty, "no frame to hang them on");
            Assert.That(design.Extensions[ShipDesignFormat.DroppedField], Does.Contain("\"hull.block\"").And.Contain("\"pos\":[0,0,-10]"));

            var again = _codec.Decode(_codec.Encode(design)).Design!;
            Assert.That(again.Extensions[ShipDesignFormat.DroppedField], Is.EqualTo(design.Extensions[ShipDesignFormat.DroppedField]), "kept through a save");
        }

        [TestCase("v2-golden.ship.json")]
        [TestCase("v3-golden.ship.json")]
        public void TheV2AndV3GoldenFiles_LoadAsTheTestDeck_TheHelmHungOnItsFloor(string file)
        {
            var result = _codec.Decode(Golden(file));

            Assert.That(result.Succeeded, Is.True, result.Error);
            var design = result.Design!;
            Assert.That(design.NextPartInstanceId, Is.EqualTo(ShipFrameTestDesigns.NextId));
            var frame = design.Frame;
            Assert.That(frame.Base, Is.EqualTo(-5));
            Assert.That(frame.StoryHeights, Is.EqualTo(new[] { 30 }));
            Assert.That(frame.Nodes.Select(n => (n.Id, n.X, n.Y, n.Z)), Is.EqualTo(new[] { (1, -20, 0, -20), (2, -20, 0, 20), (3, 20, 2, 20), (4, 20, 0, -20) }));
            Assert.That(frame.Edges.Select(e => (e.A, e.B, e.Rail)), Is.EqualTo(new[]
            {
                (1, 2, ShipFrameRailing.None), (2, 3, ShipFrameRailing.Rail), (3, 4, ShipFrameRailing.None), (4, 1, ShipFrameRailing.None),
            }), "a railing written as true is a rail");
            Assert.That(design.Parts.Single().Anchor, Is.EqualTo(ShipPartAnchor.OnFace(9, 0, 0)));

            using var parts = new ShipDesignTestParts();
            var validation = new ShipDesignValidator().Validate(design, parts.Catalog, ShipDesignLimits.Default);
            Assert.That(validation.Problems, Is.Empty, validation.ToString());
            Assert.That(_codec.Encode(design), Is.EqualTo(_codec.Encode(ShipFrameTestDesigns.Deck())), "the canonical form: the test deck");
        }

        [Test]
        public void APart_SaysWhatItHangsOn_OnItsLine()
        {
            var floor = ShipFrameTestDesigns.Deck();
            var design = floor.WithParts(floor.Parts.Append(new ShipPartPlacement(11, Post, ShipPartAnchor.OnNode(3), 2)), 12);

            string text = _codec.Encode(design);

            Assert.That(text, Does.Contain("{\"id\":10,\"part\":\"core.helm\",\"on\":{\"face\":9,\"x\":0,\"z\":0},\"rot\":0}"));
            Assert.That(text, Does.Contain("{\"id\":11,\"part\":\"hull.post\",\"on\":{\"node\":3},\"rot\":2}"));
            Assert.That(ShipDesignHash.Compute(_codec.Decode(text).Design!), Is.EqualTo(ShipDesignHash.Compute(design)));
        }

        [Test]
        public void ADesignWithAFrame_RoundTrips_StairsAndGatesIncluded_AndOneWithoutWritesNoFrame()
        {
            var well = ShipFrameTestDesigns.StairWell();
            var gated = well.With(edges: well.Edges.Select(e => e.Id == 13 ? e.WithRail(ShipFrameRailing.Gate) : e));
            var design = ShipFrameTestDesigns.FrameOnly(gated);

            string text = _codec.Encode(design);
            var back = _codec.Decode(text).Design!;

            Assert.That(text, Does.Contain("\"rail\":\"gate\"").And.Contain("\"stairs\": [").And.Contain("{\"id\":21,\"edge\":15,\"dir\":-1}"));
            Assert.That(ShipDesignHash.Compute(back), Is.EqualTo(ShipDesignHash.Compute(design)));
            Assert.That(_codec.Encode(ShipDesign.Empty("Bare")), Does.Not.Contain(ShipDesignFormat.FrameField));
        }

        [TestCase("{\"formatVersion\":4,\"parts\":[],\"frame\":[]}", "not an object")]
        [TestCase("{\"formatVersion\":4,\"parts\":[],\"frame\":{\"nodes\":[{\"id\":1,\"story\":0,\"pos\":[0,0]}]}}", "three whole numbers")]
        [TestCase("{\"formatVersion\":4,\"parts\":[],\"frame\":{\"faces\":[{\"id\":1,\"nodes\":[1,2,3],\"fill\":\"glass\"}]}}", "no fill")]
        [TestCase("{\"formatVersion\":4,\"parts\":[],\"frame\":{\"edges\":[{\"id\":1,\"a\":1}]}}", "\"b\"")]
        [TestCase("{\"formatVersion\":4,\"parts\":[],\"frame\":{\"edges\":[{\"id\":1,\"a\":1,\"b\":2,\"rail\":\"rope\"}]}}", "no railing")]
        [TestCase("{\"formatVersion\":4,\"parts\":[],\"frame\":{\"stairs\":[{\"id\":1,\"edge\":2}]}}", "\"dir\"")]
        [TestCase("{\"formatVersion\":4,\"parts\":[],\"frame\":{\"stories\":[30,30,30,30,30,30,30,30,30]}}", "stories")]
        public void BrokenFrames_AreRefused_WithAReason(string text, string reason)
        {
            var result = _codec.Decode(text);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain(reason));
        }

        [Test]
        public void ADesign_RoundTrips()
        {
            var design = Design((Helm, 0, 0, 0), (Beam, -3, 7, 2), (Block, 100, 5, 3));

            var back = _codec.Decode(_codec.Encode(design)).Design!;

            Assert.That(ShipDesignHash.Compute(back), Is.EqualTo(ShipDesignHash.Compute(design)));
            Assert.That(back.Name, Is.EqualTo("Test"));
        }

        [Test]
        public void Encoding_IsCanonical_WhateverOrderThePartsCameIn()
        {
            var design = Design((Helm, 0, 0, 0), (Block, 1, 0, 0), (Block, 2, 0, 0));
            var backward = new ShipDesign(design.Name, design.Author, design.NextPartInstanceId, design.Parts.Reverse(), frame: design.Frame);

            Assert.That(_codec.Encode(backward), Is.EqualTo(_codec.Encode(design)));
            Assert.That(_codec.Encode(design), Does.Not.Contain("\r"), "line ends are the same on every platform");
        }

        [Test]
        public void FieldsThisBuildDoesNotKnow_SurviveALoadAndSave()
        {
            const string fromTheFuture = "{\"formatVersion\":4,\"name\":\"F\",\"paint\":{\"hull\":\"#aa3300\",\"trim\":[1,2]}," +
                                         "\"parts\":[{\"id\":1,\"part\":\"core.helm\",\"on\":{\"node\":3},\"rot\":0,\"wiring\":[2,3],\"label\":\"Bridge\"}]}";

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
            var design = Design((Helm, 0, 0, 0), ("hull.from_the_future", 2, 0, 0));

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
        [TestCase("{\"formatVersion\":4}", "parts")]
        [TestCase("{\"formatVersion\":4,\"parts\":[{\"id\":1,\"on\":{\"node\":1}}]}", "\"part\"")]
        [TestCase("{\"formatVersion\":4,\"parts\":[{\"id\":1,\"part\":\"a\"}]}", "\"on\"")]
        [TestCase("{\"formatVersion\":4,\"parts\":[{\"id\":1,\"part\":\"a\",\"on\":{\"x\":0}}]}", "neither a face nor a node")]
        [TestCase("{\"formatVersion\":4,\"parts\":[{\"id\":1,\"part\":\"a\",\"on\":{\"face\":9,\"x\":0.5,\"z\":0}}]}", "whole number")]
        [TestCase("{\"formatVersion\":4,\"parts\":[{\"id\":1,\"part\":\"a\",\"on\":{\"node\":1},\"rot\":300}]}", "out of range")]
        [TestCase("{\"formatVersion\":4,\"parts\":[{\"id\":99999999999,\"part\":\"a\",\"on\":{\"node\":1}}]}", "not readable")]
        [TestCase("{\"formatVersion\":4,\"parts\":[]} {}", "not readable")]
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

            Assert.That(codec.Decode(_codec.Encode(Design((Helm, 0, 0, 0), (Block, 1, 0, 0)))).Error, Does.Contain("at most 1"));
            Assert.That(codec.Decode(new string(' ', 2000) + "{}").Error, Does.Contain("characters"));
        }
    }
}
