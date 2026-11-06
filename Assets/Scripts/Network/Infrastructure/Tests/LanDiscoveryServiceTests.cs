#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Networking;
using TinCan.Network.Infrastructure.Session;
using TinCan.Tests.Fakes;

namespace TinCan.Network.Infrastructure.Tests
{
    public class LanDiscoveryServiceTests
    {
        private const string Version = "0.4.2";
        private const int Port = 47777;

        private FakeSessionService _session = null!;
        private FakeNetworkConnections _connections = null!;
        private FakeDatagramSockets _sockets = null!;
        private FakeTimeService _time = null!;
        private LanDiscoveryService _discovery = null!;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _session = new FakeSessionService();
            _connections = new FakeNetworkConnections { ListenPort = 7777 };
            _sockets = new FakeDatagramSockets();
            _time = new FakeTimeService { Time = 10f };
            _discovery = new LanDiscoveryService(_session, _connections, _sockets, _time, new FakeEventPublisher(), Version, Port);
            _changes = 0;
            _discovery.Changed += () => _changes++;
        }

        [TearDown]
        public void TearDown() => _discovery.Dispose();

        private static byte[] Reply(uint hostId, string name, int players = 1, string version = Version, ushort gamePort = 7777) =>
            LanDiscoveryProcessor.Reply(new LanGameInfo(hostId, name, version, gamePort, players, 8, false));

        private void Host(string name = "Ada's game", int maxPlayers = 8)
        {
            _session.IsActive = true;
            _session.Hosted = new HostedGame(name, maxPlayers);
        }

        [Test]
        public void NotServing_OpensNoPort()
        {
            _discovery.Tick();

            Assert.That(_sockets.ListenCalls, Is.Zero);
        }

        [Test]
        public void Serving_AnswersAQuery_ToTheAsker()
        {
            Host("Ada's game", maxPlayers: 4);
            _discovery.Tick();
            Assert.That(_sockets.Listener!.Port, Is.EqualTo(Port));

            _sockets.Listener.Deliver(LanDiscoveryProcessor.Query(), "192.168.1.9", 50123);
            _discovery.Tick();

            var sent = _sockets.Listener.Sent.Single();
            Assert.That(sent.Address, Is.EqualTo("192.168.1.9"));
            Assert.That(sent.Port, Is.EqualTo(50123));
            Assert.That(LanDiscoveryProcessor.TryReadReply(sent.Bytes, out var info), Is.True);
            Assert.That(info.Name, Is.EqualTo("Ada's game"));
            Assert.That(info.MaxPlayers, Is.EqualTo(4));
            Assert.That(info.GamePort, Is.EqualTo(7777));
            Assert.That(info.Version, Is.EqualTo(Version));
        }

        [Test]
        public void Serving_IgnoresAnythingButAQuery()
        {
            Host();
            _discovery.Tick();

            _sockets.Listener!.Deliver(new byte[] { 1, 2, 3 }, "192.168.1.9", 50123);
            _sockets.Listener.Deliver(Reply(7, "Bo's game"), "192.168.1.9", 50123);
            _discovery.Tick();

            Assert.That(_sockets.Listener.Sent, Is.Empty);
        }

        [Test]
        public void StoppingServing_ClosesThePort()
        {
            Host();
            _discovery.Tick();
            var listener = _sockets.Listener!;

            _session.Hosted = null;
            _discovery.Tick();

            Assert.That(listener.Disposed, Is.True);
        }

        [Test]
        public void PortTaken_TriesOncePerSession()
        {
            _sockets.ListenFails = true;
            Host();

            _discovery.Tick();
            _discovery.Tick();

            Assert.That(_sockets.ListenCalls, Is.EqualTo(1));
        }

        [Test]
        public void Browsing_AsksByBroadcastAndLoopback_OncePerInterval()
        {
            _discovery.Browse(true);
            _discovery.Tick();
            _time.Time += 0.5f;
            _discovery.Tick();

            var sent = _sockets.Broadcasting!.Sent;
            Assert.That(sent.Select(s => s.Address), Is.EqualTo(new[] { LanDiscoveryService.BroadcastAddress, LanDiscoveryService.LoopbackAddress }));
            Assert.That(sent.All(s => s.Port == Port && LanDiscoveryProcessor.IsQuery(s.Bytes)), Is.True);

            _time.Time += LanDiscoveryService.QueryInterval;
            _discovery.Tick();
            Assert.That(sent.Count, Is.EqualTo(4));
        }

        [Test]
        public void Browsing_ListsWhoAnswered_AtTheirAddress()
        {
            _discovery.Browse(true);
            _discovery.Tick();

            _sockets.Broadcasting!.Deliver(Reply(7, "Bo's game", players: 2), "192.168.1.20", Port);
            _discovery.Tick();

            var game = _discovery.Games.Single();
            Assert.That(game.Name, Is.EqualTo("Bo's game"));
            Assert.That(game.Target.ToString(), Is.EqualTo("192.168.1.20:7777"));
            Assert.That(game.Players, Is.EqualTo(2));
            Assert.That(game.SameVersion, Is.True);
            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void AHostHeardTwice_IsOneGame_AtItsLoopbackAddress()
        {
            _discovery.Browse(true);
            _discovery.Tick();

            _sockets.Broadcasting!.Deliver(Reply(7, "Bo's game"), "192.168.1.20", Port);
            _sockets.Broadcasting.Deliver(Reply(7, "Bo's game"), LanDiscoveryService.LoopbackAddress, Port);
            _sockets.Broadcasting.Deliver(Reply(7, "Bo's game"), "192.168.1.20", Port);
            _discovery.Tick();

            Assert.That(_discovery.Games.Single().Address, Is.EqualTo(LanDiscoveryService.LoopbackAddress));
        }

        [Test]
        public void AnUnchangedAnswer_RaisesNothing()
        {
            _discovery.Browse(true);
            _discovery.Tick();
            _sockets.Broadcasting!.Deliver(Reply(7, "Bo's game"), "192.168.1.20", Port);
            _discovery.Tick();

            _sockets.Broadcasting.Deliver(Reply(7, "Bo's game"), "192.168.1.20", Port);
            _discovery.Tick();
            _sockets.Broadcasting.Deliver(Reply(7, "Bo's game", players: 3), "192.168.1.20", Port);
            _discovery.Tick();

            Assert.That(_changes, Is.EqualTo(2), "found, then the player count changed");
        }

        [Test]
        public void AnotherVersion_IsListed_AsNotTheSame()
        {
            _discovery.Browse(true);
            _discovery.Tick();

            _sockets.Broadcasting!.Deliver(Reply(7, "Old game", version: "0.3.9"), "192.168.1.20", Port);
            _discovery.Tick();

            Assert.That(_discovery.Games.Single().SameVersion, Is.False);
        }

        [Test]
        public void AGameThatStopsAnswering_IsForgotten()
        {
            _discovery.Browse(true);
            _discovery.Tick();
            _sockets.Broadcasting!.Deliver(Reply(7, "Bo's game"), "192.168.1.20", Port);
            _discovery.Tick();

            _time.Time += LanDiscoveryService.StaleAfter + 0.1f;
            _discovery.Tick();

            Assert.That(_discovery.Games, Is.Empty);
            Assert.That(_changes, Is.EqualTo(2));
        }

        [Test]
        public void StoppingBrowsing_ClosesTheSocket_AndForgetsTheList()
        {
            _discovery.Browse(true);
            _discovery.Tick();
            var browser = _sockets.Broadcasting!;
            browser.Deliver(Reply(7, "Bo's game"), "192.168.1.20", Port);
            _discovery.Tick();

            _discovery.Browse(false);

            Assert.That(browser.Disposed, Is.True);
            Assert.That(_discovery.Games, Is.Empty);
            Assert.That(_discovery.Browsing, Is.False);
            Assert.That(_changes, Is.EqualTo(2));
        }
    }
}
