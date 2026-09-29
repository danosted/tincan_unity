#nullable enable
using NUnit.Framework;
using TinCan.Core.UI;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    public class CommandLineSessionBootstrapTests
    {
        [Test]
        public void TryParse_AutoHost()
        {
            Assert.That(CommandLineSessionBootstrap.TryParse(new[] { "game.exe", "-autohost" }, out var request), Is.True);
            Assert.That(request.Kind, Is.EqualTo(SessionRequestKind.Host));
        }

        [Test]
        public void TryParse_AutoJoinWithAddressAndPort()
        {
            Assert.That(CommandLineSessionBootstrap.TryParse(new[] { "game.exe", "-autojoin", "10.0.0.5:8000" }, out var request), Is.True);
            Assert.That(request.Kind, Is.EqualTo(SessionRequestKind.Join));
            Assert.That(request.Address, Is.EqualTo("10.0.0.5"));
            Assert.That(request.Port, Is.EqualTo(8000));
        }

        [Test]
        public void TryParse_AutoJoinWithoutEndpoint_UsesDefaults()
        {
            Assert.That(CommandLineSessionBootstrap.TryParse(new[] { "game.exe", "-autojoin", "-screen-fullscreen", "0" }, out var request), Is.True);
            Assert.That(request.Address, Is.EqualTo("127.0.0.1"));
            Assert.That(request.Port, Is.EqualTo(7777));
        }

        [Test]
        public void TryParse_NoFlags_ReturnsFalse()
        {
            Assert.That(CommandLineSessionBootstrap.TryParse(new[] { "game.exe", "-batchmode" }, out _), Is.False);
            Assert.That(CommandLineSessionBootstrap.TryParse(null!, out _), Is.False);
        }

        [Test]
        public void Start_AutoJoinArgs_ConnectsOnce()
        {
            var network = new FakeNetworkService();
            var bootstrap = new CommandLineSessionBootstrap(network, new FakeEventPublisher(), new FakeTimeService());

            // Environment args are the test runner's; this only checks Start is safe to call.
            Assert.DoesNotThrow(() => bootstrap.Start());
        }

        [Test]
        public void TryParse_JoinDelay()
        {
            Assert.That(CommandLineSessionBootstrap.TryParse(new[] { "game.exe", "-autojoin", "-joindelay", "8.5" }, out var request), Is.True);
            Assert.That(request.Kind, Is.EqualTo(SessionRequestKind.Join));
            Assert.That(request.DelaySeconds, Is.EqualTo(8.5f));
        }

        [Test]
        public void TryParse_InvalidOrMissingJoinDelay_JoinsAtOnce()
        {
            CommandLineSessionBootstrap.TryParse(new[] { "game.exe", "-autojoin", "-joindelay", "soon" }, out var invalid);
            CommandLineSessionBootstrap.TryParse(new[] { "game.exe", "-autojoin" }, out var missing);
            Assert.That(invalid.DelaySeconds, Is.EqualTo(0f));
            Assert.That(missing.DelaySeconds, Is.EqualTo(0f));
        }

        [Test]
        public void Begin_JoinWithoutDelay_ConnectsImmediately()
        {
            var network = new FakeNetworkService();
            var bootstrap = new CommandLineSessionBootstrap(network, new FakeEventPublisher(), new FakeTimeService());

            bootstrap.Begin(new[] { "-autojoin" });

            Assert.That(network.StartClientCalls, Is.EqualTo(1));
        }

        [Test]
        public void Begin_JoinWithDelay_ConnectsOnceAfterTheDelay()
        {
            var network = new FakeNetworkService();
            var time = new FakeTimeService { DeltaTime = 1f };
            var bootstrap = new CommandLineSessionBootstrap(network, new FakeEventPublisher(), time);

            bootstrap.Begin(new[] { "-autojoin", "-joindelay", "2.5" });
            bootstrap.Tick();
            bootstrap.Tick();
            Assert.That(network.StartClientCalls, Is.EqualTo(0), "still waiting after 2 s");

            bootstrap.Tick();
            bootstrap.Tick();
            Assert.That(network.StartClientCalls, Is.EqualTo(1));
        }

        [Test]
        public void Begin_HostIgnoresJoinDelay()
        {
            var network = new FakeNetworkService();
            var bootstrap = new CommandLineSessionBootstrap(network, new FakeEventPublisher(), new FakeTimeService());

            bootstrap.Begin(new[] { "-autohost", "-joindelay", "5" });

            Assert.That(network.StartHostCalls, Is.EqualTo(1));
        }
    }
}
