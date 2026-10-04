#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipDesignBinaryCodec"/> (the network form) and <see cref="ShipDesignHash"/>.</summary>
    public class ShipDesignBinaryCodecTests
    {
        private static ShipDesign Sample() =>
            Design((Helm, 0, 1, 0, 0), (Beam, -1, 0, 0, 0), (Block, -300, 7, 128, 23), (Block, 0, 0, -1, 3));

        [Test]
        public void ADesign_RoundTrips()
        {
            var design = Sample();

            var back = ShipDesignBinaryCodec.Decode(ShipDesignBinaryCodec.Encode(design), ShipDesignLimits.Default).Design!;

            Assert.That((back.Name, back.Author, back.NextPartInstanceId), Is.EqualTo((design.Name, design.Author, design.NextPartInstanceId)));
            Assert.That(back.Parts.Select(p => p.ToString()), Is.EqualTo(design.Parts.Select(p => p.ToString())));
        }

        [Test]
        public void TheFileAndTheNetwork_CarryTheSameDesign()
        {
            var json = new ShipDesignJsonCodec(ShipDesignLimits.Default);
            var design = Sample();

            var viaFile = json.Decode(json.Encode(design)).Design!;
            var viaNetwork = ShipDesignBinaryCodec.Decode(ShipDesignBinaryCodec.Encode(viaFile), ShipDesignLimits.Default).Design!;

            Assert.That(json.Encode(viaNetwork), Is.EqualTo(json.Encode(design)));
        }

        [Test]
        public void ItIsCompact()
        {
            var parts = Enumerable.Range(0, 500).Select(i => (Block, i % 20, i / 100, i / 20 % 5, (byte)0)).ToArray();

            int bytes = ShipDesignBinaryCodec.Encode(Design(parts)).Length;

            Assert.That(bytes, Is.LessThan(500 * 8), $"{bytes} bytes for 500 parts");
        }

        [Test]
        public void TruncatedOrPaddedBytes_AreRefused()
        {
            var bytes = ShipDesignBinaryCodec.Encode(Sample());

            Assert.That(ShipDesignBinaryCodec.Decode(bytes.Take(bytes.Length - 1).ToArray(), ShipDesignLimits.Default).Succeeded, Is.False);
            Assert.That(ShipDesignBinaryCodec.Decode(bytes.Append((byte)0).ToArray(), ShipDesignLimits.Default).Succeeded, Is.False);
            Assert.That(ShipDesignBinaryCodec.Decode(new byte[] { 9 }, ShipDesignLimits.Default).Error, Does.Contain("version"));
        }

        [Test]
        public void TooManyParts_AreRefusedBeforeReadingThem()
        {
            var limits = ShipDesignLimits.Default;
            limits.MaxParts = 3; // three part types fit, four parts do not

            Assert.That(ShipDesignBinaryCodec.Decode(ShipDesignBinaryCodec.Encode(Sample()), limits).Error, Does.Contain("at most 3"));
        }

        [Test]
        public void TheHash_IgnoresPartOrder_ButNotContent()
        {
            var design = Sample();
            var reordered = new ShipDesign(design.Name, design.Author, design.NextPartInstanceId, design.Parts.Reverse());
            var moved = Design((Helm, 0, 1, 0, 0), (Beam, -1, 0, 0, 0), (Block, -300, 7, 128, 23), (Block, 0, 0, -1, 2));

            Assert.That(ShipDesignHash.Compute(reordered), Is.EqualTo(ShipDesignHash.Compute(design)));
            Assert.That(ShipDesignHash.Compute(moved), Is.Not.EqualTo(ShipDesignHash.Compute(design)));
            Assert.That(ShipDesignHash.Compute(design.WithName("Other")), Is.Not.EqualTo(ShipDesignHash.Compute(design)));
        }

        [Test]
        public void TheHash_IsPinned_SoItIsTheSameOnEveryPlatformAndBuild()
        {
            // If the network form changes on purpose, update this value; every peer must agree on it.
            Assert.That(ShipDesignHash.ToText(ShipDesignHash.Compute(Sample())), Is.EqualTo("d2a261783aea4373"));
            Assert.That(ShipDesignHash.ToText(0x0123456789abcdefUL), Is.EqualTo("0123456789abcdef"));
        }
    }
}
