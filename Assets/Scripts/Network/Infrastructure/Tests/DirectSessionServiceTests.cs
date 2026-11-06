#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TinCan.Core.Domain.Networking;
using TinCan.Network.Infrastructure.Session;
using TinCan.Tests.Fakes;

namespace TinCan.Network.Infrastructure.Tests
{
    public class DirectSessionServiceTests
    {
        private FakeNetworkService _network = null!;
        private FakeNetworkConnections _connections = null!;
        private FakePlayerSettings _settings = null!;
        private DirectSessionService _session = null!;

        [SetUp]
        public void SetUp()
        {
            _network = new FakeNetworkService();
            _connections = new FakeNetworkConnections();
            _settings = new FakePlayerSettings();
            _session = FakeSessions.Direct(_network, _connections, _settings);
        }

        [TearDown]
        public void TearDown() => _session.Dispose();

        private static byte[] Payload(string name, string version = FakeSessions.Version) => new ConnectionPayload(version, name).Encode();

        [Test]
        public void Host_SendsThisBuildsVersionAndThePlayersName_ThenStartsAHost()
        {
            _settings.Set(DirectSessionService.PlayerNameSettingKey, "Ada");

            _session.HostAsync(HostOptions.Default, default);

            Assert.That(_network.StartHostCalls, Is.EqualTo(1));
            Assert.That(ConnectionPayload.TryDecode(_connections.Payload, out var payload), Is.True);
            Assert.That(payload.Version, Is.EqualTo(FakeSessions.Version));
            Assert.That(payload.Name, Is.EqualTo("Ada"));
        }

        [Test]
        public async Task Host_ThatNeverComesUp_Fails()
        {
            // This fake never becomes active, like a host whose port is taken.
            var start = await _session.HostAsync(HostOptions.Default, default);

            Assert.That(start.Kind, Is.EqualTo(SessionStartKind.Failed));
            Assert.That(start.Message, Is.EqualTo(DirectSessionService.HostFailedMessage));
        }

        [Test]
        public async Task Host_WhileActive_StartsNothing()
        {
            var network = new FakeSessionNetworkService { IsActive = true, IsServer = true, IsClient = true };
            var session = FakeSessions.Direct(network);

            var start = await session.HostAsync(HostOptions.Default, default);

            Assert.That(start.Kind, Is.EqualTo(SessionStartKind.AlreadyActive));
        }

        [TestCase("", false, "Ada's game")]
        [TestCase("  Deck crew  ", false, "Deck crew")]
        [TestCase("", true, DirectSessionService.DefaultServerName)]
        public async Task Hosting_NamesTheGame_ForOtherPlayersLists(string name, bool dedicated, string expected)
        {
            var settings = new FakePlayerSettings();
            settings.Set(DirectSessionService.PlayerNameSettingKey, "Ada");
            var network = new FakeSessionNetworkService { IsActive = false, IsServer = true, IsClient = !dedicated };
            var session = FakeSessions.Direct(network, settings: settings);

            await session.HostAsync(new HostOptions(maxPlayers: 4, dedicated: dedicated, name: name), default);

            Assert.That(session.Hosted!.Value.Name, Is.EqualTo(expected));
            Assert.That(session.Hosted.Value.MaxPlayers, Is.EqualTo(4));
        }

        [Test]
        public void Hosted_IsNull_OnAClientAndOffline()
        {
            var network = new FakeSessionNetworkService { IsActive = false, IsClient = true };
            var session = FakeSessions.Direct(network);
            Assert.That(session.Hosted, Is.Null);

            _ = session.JoinAsync(new JoinTarget("10.0.0.5", 7777), default);

            Assert.That(session.Hosted, Is.Null);
        }

        [Test]
        public void WithoutANameSetting_SendsTheDefaultName()
        {
            _session.JoinAsync(new JoinTarget("10.0.0.5", 7777), default);

            ConnectionPayload.TryDecode(_connections.Payload, out var payload);
            Assert.That(payload.Name, Is.EqualTo(ConnectionPayload.DefaultName));
        }

        [Test]
        public void Join_ConnectsToTheTarget()
        {
            _session.JoinAsync(new JoinTarget("10.0.0.5", 8000), default);

            Assert.That(_network.LastAddress, Is.EqualTo("10.0.0.5"));
            Assert.That(_network.LastPort, Is.EqualTo(8000));
            Assert.That(_network.StartClientCalls, Is.EqualTo(1));
        }

        [TestCase("10.0.0.5", "10.0.0.5")]
        [TestCase("", DirectSessionService.DefaultListenAddress)]
        public void Dedicated_ListensOnTheEndpoint_ThenStartsAServer(string address, string expected)
        {
            _session.HostAsync(new HostOptions(dedicated: true, listenAddress: address, port: 9000), default);

            Assert.That(_network.LastListenAddress, Is.EqualTo(expected));
            Assert.That(_network.LastListenPort, Is.EqualTo(9000));
            Assert.That(_network.StartServerCalls, Is.EqualTo(1));
            Assert.That(_network.StartHostCalls, Is.Zero);
        }

        [Test]
        public void Players_JoinAndLeave_WithTheirNames()
        {
            var joined = new List<string>();
            var left = new List<string>();
            _session.PlayerJoined += p => joined.Add(p.Name);
            _session.PlayerLeft += p => left.Add(p.Name);
            _session.HostAsync(HostOptions.Default, default);

            _connections.Connect(0, isHostClient: true);
            _connections.Connect(1, Payload("Bo"));
            _connections.Disconnect(1);

            Assert.That(joined, Is.EqualTo(new[] { ConnectionPayload.DefaultName, "Bo" }));
            Assert.That(left, Is.EqualTo(new[] { "Bo" }));
            Assert.That(_session.Players.Select(p => p.ClientId), Is.EqualTo(new ulong[] { 0 }));
        }

        [Test]
        public void FullGame_RefusesTheNextClient_UntilSomeoneLeaves()
        {
            _session.HostAsync(new HostOptions(maxPlayers: 2), default);
            _connections.Connect(0, isHostClient: true);
            _connections.Connect(1, Payload("Bo"));

            var refused = _connections.Connect(2, Payload("Cy"));
            _connections.Disconnect(1);
            var afterLeave = _connections.Connect(3, Payload("Di"));

            Assert.That(refused.Approved, Is.False);
            Assert.That(refused.Reason, Is.EqualTo(ConnectionApprovalProcessor.FullReason(2)));
            Assert.That(afterLeave.Approved, Is.True);
        }

        [Test]
        public void OtherVersion_IsRefused_AndNeverJoins()
        {
            int joined = 0;
            _session.PlayerJoined += _ => joined++;
            _session.HostAsync(HostOptions.Default, default);

            var decision = _connections.Connect(1, Payload("Bo", "0.0.1"));

            Assert.That(decision.Approved, Is.False);
            Assert.That(joined, Is.Zero);
        }

        [Test]
        public void Dispose_StopsApproving()
        {
            _session.Dispose();

            Assert.That(_connections.Approval, Is.Null);
        }
    }
}
