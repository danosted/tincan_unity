#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TinCan.Core.Domain.Networking;
using TinCan.Network.Infrastructure.Session;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    /// <summary>The client side: how a join goes, and how a running session ends.</summary>
    public class DirectSessionServiceEndTests
    {
        private const ulong Local = 5;
        private static readonly JoinTarget Target = new("10.0.0.5", 7777);

        private FakeSessionNetworkService _network = null!;
        private FakeNetworkConnections _connections = null!;
        private DirectSessionService _session = null!;
        private List<SessionEnd> _ends = null!;

        [SetUp]
        public void SetUp()
        {
            _network = new FakeSessionNetworkService { IsActive = false, IsClient = true, LocalClientId = Local };
            _connections = new FakeNetworkConnections();
            _session = FakeSessions.Direct(_network, _connections);
            _ends = new List<SessionEnd>();
            _session.Ended += _ends.Add;
        }

        [TearDown]
        public void TearDown() => _session.Dispose();

        private static async Task AssertCancelled(UniTask<SessionStart> join)
        {
            try
            {
                await join;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            Assert.Fail("The join completed instead of being cancelled.");
        }

        private async Task Joined()
        {
            var join = _session.JoinAsync(Target, default);
            _connections.ConnectedAsClient(Local);
            Assert.That((await join).IsStarted, Is.True);
        }

        [Test]
        public async Task Join_WaitsForTheHost_ThenStarts()
        {
            var join = _session.JoinAsync(Target, default);
            Assert.That(join.Status, Is.EqualTo(UniTaskStatus.Pending));

            _connections.ConnectedAsClient(Local);

            Assert.That((await join).Kind, Is.EqualTo(SessionStartKind.Started));
        }

        [Test]
        public async Task Join_Refused_GivesTheHostsReason_AndIsNoSessionEnd()
        {
            var join = _session.JoinAsync(Target, default);

            _connections.Disconnect(Local, "The game is full (8 players).");

            var start = await join;
            Assert.That(start.Kind, Is.EqualTo(SessionStartKind.Refused));
            Assert.That(start.Message, Is.EqualTo("The game is full (8 players)."));
            Assert.That(_ends, Is.Empty);
            Assert.That(_session.LastEnd, Is.Null);
        }

        [Test]
        public async Task Join_DroppedWithoutAReason_CouldNotReach()
        {
            var join = _session.JoinAsync(Target, default);

            _connections.Disconnect(Local);

            var start = await join;
            Assert.That(start.Kind, Is.EqualTo(SessionStartKind.Failed));
            Assert.That(start.Message, Is.EqualTo(DirectSessionService.UnreachableMessage(Target)));
        }

        [Test]
        public async Task Join_Cancelled_ShutsTheAttemptDown()
        {
            using var cancel = new CancellationTokenSource();
            var join = _session.JoinAsync(Target, cancel.Token);

            cancel.Cancel();

            await AssertCancelled(join);
            Assert.That(_network.IsActive, Is.False);
            Assert.That(_ends, Is.Empty);
        }

        [Test]
        public async Task Leave_WhileJoining_CancelsTheJoin()
        {
            var join = _session.JoinAsync(Target, default);

            _session.Leave();

            await AssertCancelled(join);
            Assert.That(_ends, Is.Empty);
        }

        [Test]
        public async Task Join_WhileJoining_StartsNothing()
        {
            _ = _session.JoinAsync(Target, default);
            _network.IsActive = false;

            var second = await _session.JoinAsync(Target, default);

            Assert.That(second.Kind, Is.EqualTo(SessionStartKind.AlreadyActive));
        }

        [Test]
        public async Task RunningSession_RemovedWithAReason_EndsRefused()
        {
            await Joined();

            _connections.Disconnect(Local, "Kicked by the host.");

            Assert.That(_ends.Single().Kind, Is.EqualTo(SessionEndKind.Refused));
            Assert.That(_session.LastEnd!.Value.Message, Is.EqualTo("Kicked by the host."));
        }

        [Test]
        public async Task RunningSession_DroppedWithoutAReason_IsConnectionLost()
        {
            await Joined();

            _connections.Disconnect(Local);

            Assert.That(_ends.Single().Kind, Is.EqualTo(SessionEndKind.ConnectionLost));
            Assert.That(_ends.Single().Message, Is.EqualTo(SessionEnd.ConnectionLostMessage));
        }

        [Test]
        public async Task Leave_EndsOnce_EvenWhenOurOwnDisconnectFollows()
        {
            await Joined();

            _session.Leave();
            _connections.Disconnect(Local);

            Assert.That(_ends.Single().Kind, Is.EqualTo(SessionEndKind.Left));
            Assert.That(_ends.Single().Message, Is.Empty);
        }

        [Test]
        public async Task ANewSession_ClearsTheLastEnd()
        {
            await Joined();
            _connections.Disconnect(Local);
            _network.IsActive = false;

            _ = _session.JoinAsync(Target, default);

            Assert.That(_session.LastEnd, Is.Null);
        }
    }
}
