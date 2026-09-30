#nullable enable
using System.IO;
using NUnit.Framework;
using TinCan.DevTools.Editor;

namespace TinCan.Tests.EditMode
{
    /// <summary>.docs/INPUT_MAP.md is generated; it must match the assets and code it describes.</summary>
    public class InputMapTests
    {
        [Test]
        public void InputMap_IsCurrent()
        {
            var path = Path.Combine(InputMapWriter.ProjectRoot, InputMapWriter.DocPath);
            Assert.That(File.Exists(path), Is.True, $"Run TinCan > Dev > Input > Write Input Map to create {InputMapWriter.DocPath}.");

            var written = File.ReadAllText(path).Replace("\r\n", "\n");
            Assert.That(written, Is.EqualTo(InputMapWriter.Build()),
                $"{InputMapWriter.DocPath} is stale: run TinCan > Dev > Input > Write Input Map and commit the result.");
        }

        [Test]
        public void EveryRoutedCommand_HasAHandler()
        {
            Assert.That(InputMapWriter.Build(), Does.Not.Contain("**none**"), "a context routes to a command no InputCommandHandler handles");
        }
    }
}
