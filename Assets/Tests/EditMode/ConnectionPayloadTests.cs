#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Network.Infrastructure.Session;

namespace TinCan.Tests.EditMode
{
    public class ConnectionPayloadTests
    {
        [Test]
        public void EncodeThenDecode_GivesBackVersionAndName()
        {
            var bytes = new ConnectionPayload("0.4.2", "Ada Lovelace").Encode();

            Assert.That(ConnectionPayload.TryDecode(bytes, out var payload), Is.True);
            Assert.That(payload.Version, Is.EqualTo("0.4.2"));
            Assert.That(payload.Name, Is.EqualTo("Ada Lovelace"));
        }

        [Test]
        public void TryDecode_EmptyOtherFormatOrTruncated_IsFalse()
        {
            var bytes = new ConnectionPayload("0.4.2", "Ada").Encode();
            var truncated = bytes.Take(bytes.Length - 2).ToArray();
            var otherFormat = (byte[])bytes.Clone();
            otherFormat[0] = 99;

            Assert.That(ConnectionPayload.TryDecode(null, out _), Is.False);
            Assert.That(ConnectionPayload.TryDecode(new byte[0], out _), Is.False);
            Assert.That(ConnectionPayload.TryDecode(otherFormat, out _), Is.False);
            Assert.That(ConnectionPayload.TryDecode(truncated, out _), Is.False);
        }

        [TestCase(null, ConnectionPayload.DefaultName)]
        [TestCase("", ConnectionPayload.DefaultName)]
        [TestCase("   ", ConnectionPayload.DefaultName)]
        [TestCase("  Bo  ", "Bo")]
        [TestCase("Bo\n\tby", "Boby")]
        [TestCase("\u0007", ConnectionPayload.DefaultName)]
        [TestCase("Abcdefghijklmnopqrstuvwxyz", "Abcdefghijklmnopqrstuvwx")]
        public void CleanName(string? name, string expected)
        {
            Assert.That(ConnectionPayload.CleanName(name), Is.EqualTo(expected));
        }
    }
}
