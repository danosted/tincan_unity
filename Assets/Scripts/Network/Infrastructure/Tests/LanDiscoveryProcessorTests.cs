#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Network.Infrastructure.Session;

namespace TinCan.Network.Infrastructure.Tests
{
    public class LanDiscoveryProcessorTests
    {
        private static readonly LanGameInfo Ada = new(42, "Ada's game", "0.4.2", 7777, players: 2, maxPlayers: 8, needsPassword: false);

        [Test]
        public void Query_IsAQuery_AndNoReply()
        {
            var query = LanDiscoveryProcessor.Query();

            Assert.That(LanDiscoveryProcessor.IsQuery(query), Is.True);
            Assert.That(LanDiscoveryProcessor.TryReadReply(query, out _), Is.False);
        }

        [Test]
        public void Reply_ReadsBackWhatTheHostSaid()
        {
            var bytes = LanDiscoveryProcessor.Reply(Ada);

            Assert.That(LanDiscoveryProcessor.IsQuery(bytes), Is.False);
            Assert.That(LanDiscoveryProcessor.TryReadReply(bytes, out var info), Is.True);
            Assert.That(info.HostId, Is.EqualTo(42));
            Assert.That(info.Name, Is.EqualTo("Ada's game"));
            Assert.That(info.Version, Is.EqualTo("0.4.2"));
            Assert.That(info.GamePort, Is.EqualTo(7777));
            Assert.That(info.Players, Is.EqualTo(2));
            Assert.That(info.MaxPlayers, Is.EqualTo(8));
            Assert.That(info.NeedsPassword, Is.False);
        }

        [Test]
        public void Reply_CutsALongName()
        {
            var bytes = LanDiscoveryProcessor.Reply(new LanGameInfo(1, new string('x', 200), "v", 7777, 1, 8, false));

            LanDiscoveryProcessor.TryReadReply(bytes, out var info);
            Assert.That(info.Name.Length, Is.EqualTo(LanDiscoveryProcessor.MaxNameLength));
        }

        [Test]
        public void Garbage_TruncatedOrOtherFormat_IsIgnored()
        {
            var reply = LanDiscoveryProcessor.Reply(Ada);
            var otherFormat = (byte[])reply.Clone();
            otherFormat[4] = 99;

            Assert.That(LanDiscoveryProcessor.TryReadReply(null, out _), Is.False);
            Assert.That(LanDiscoveryProcessor.TryReadReply(new byte[] { 1, 2, 3 }, out _), Is.False);
            Assert.That(LanDiscoveryProcessor.TryReadReply(reply.Take(reply.Length - 3).ToArray(), out _), Is.False);
            Assert.That(LanDiscoveryProcessor.TryReadReply(otherFormat, out _), Is.False);
            Assert.That(LanDiscoveryProcessor.IsQuery(new byte[] { 0, 0, 0, 0, 1 }), Is.False);
        }

        [TestCase(10f, 13f, false)]
        [TestCase(10f, 13.6f, true)]
        public void IsStale_AfterTheTimeout(float lastSeen, float now, bool stale)
        {
            Assert.That(LanDiscoveryProcessor.IsStale(lastSeen, now, 3.5f), Is.EqualTo(stale));
        }
    }
}
