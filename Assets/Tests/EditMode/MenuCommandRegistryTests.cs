#nullable enable
using NUnit.Framework;
using TinCan.Features.UI;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    public class MenuCommandRegistryTests
    {
        [Test]
        public void TryGetCommand_FindsRegisteredCommandById()
        {
            var host = new FakeMenuCommand("StartHost");
            var registry = new MenuCommandRegistry(new IMenuCommand[] { host, new FakeMenuCommand("Quit") });

            Assert.That(registry.TryGetCommand("StartHost", out var found), Is.True);
            Assert.That(found, Is.SameAs(host));
        }

        [Test]
        public void TryGetCommand_UnknownOrEmptyId_ReturnsFalse()
        {
            var registry = new MenuCommandRegistry(new IMenuCommand[] { new FakeMenuCommand("StartHost"), new FakeMenuCommand("") });

            Assert.That(registry.TryGetCommand("Nope", out _), Is.False);
            Assert.That(registry.TryGetCommand("", out _), Is.False);
        }
    }
}
