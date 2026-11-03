#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Networking;
using TinCan.Network.Infrastructure.Session;

namespace TinCan.Tests.EditMode
{
    public class ConnectionApprovalProcessorTests
    {
        private const string Version = "0.4.2";
        private readonly ConnectionApprovalProcessor _processor = new();

        private static ConnectionRequest Client(string version, string name = "Bo", ulong id = 1) =>
            new(id, false, new ConnectionPayload(version, name).Encode());

        [Test]
        public void SameVersion_WithRoom_IsApproved_WithTheSentName()
        {
            var (decision, payload) = _processor.Decide(Client(Version, "Bo"), Version, maxPlayers: 4, playersIn: 1);

            Assert.That(decision.Approved, Is.True);
            Assert.That(payload.Name, Is.EqualTo("Bo"));
        }

        [Test]
        public void OtherVersion_IsRefused_NamingBothVersions()
        {
            var (decision, _) = _processor.Decide(Client("0.3.9"), Version, 4, 1);

            Assert.That(decision.Approved, Is.False);
            Assert.That(decision.Reason, Is.EqualTo(ConnectionApprovalProcessor.VersionMismatchReason(Version, "0.3.9")));
            Assert.That(decision.Reason, Does.Contain("0.3.9").And.Contain(Version));
        }

        [Test]
        public void Full_IsRefused()
        {
            var (decision, _) = _processor.Decide(Client(Version), Version, maxPlayers: 2, playersIn: 2);

            Assert.That(decision.Approved, Is.False);
            Assert.That(decision.Reason, Is.EqualTo(ConnectionApprovalProcessor.FullReason(2)));
        }

        [Test]
        public void UnreadablePayload_IsRefused()
        {
            var (decision, payload) = _processor.Decide(new ConnectionRequest(1, false, new byte[] { 7, 7 }), Version, 4, 1);

            Assert.That(decision.Approved, Is.False);
            Assert.That(decision.Reason, Is.EqualTo(ConnectionApprovalProcessor.UnreadableReason));
            Assert.That(payload.Name, Is.EqualTo(ConnectionPayload.DefaultName));
        }

        [Test]
        public void HostClient_IsAlwaysApproved()
        {
            var unreadable = new ConnectionRequest(0, true, null);
            var full = new ConnectionRequest(0, true, new ConnectionPayload("other", "Host").Encode());

            Assert.That(_processor.Decide(unreadable, Version, 1, 5).Decision.Approved, Is.True);
            Assert.That(_processor.Decide(full, Version, 1, 5).Decision.Approved, Is.True);
        }
    }
}
