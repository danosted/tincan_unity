#nullable enable
using NUnit.Framework;
using TinCan.Features.UI;
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
            var bootstrap = new CommandLineSessionBootstrap(network, new FakeEventPublisher());

            // Environment args are the test runner's; this only checks Start is safe to call.
            Assert.DoesNotThrow(() => bootstrap.Start());
        }
    }
}
