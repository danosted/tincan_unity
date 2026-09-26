#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain;

namespace TinCan.Tests.EditMode
{
    public class LaunchArgumentsTests
    {
        [Test]
        public void Merge_TagsBecomeFlagsAfterCommandLine()
        {
            var args = LaunchArguments.Merge(new[] { "game.exe" }, new[] { "autohost", "bot:DeckWalk", "netsim=Lag100" });

            Assert.That(args, Is.EqualTo(new[] { "game.exe", "-autohost", "-bot", "DeckWalk", "-netsim", "Lag100" }));
        }

        [Test]
        public void Merge_KeepsDashedTagsAndSkipsBlanks()
        {
            var args = LaunchArguments.Merge(null, new[] { "-telemetry", " ", "" });

            Assert.That(args, Is.EqualTo(new[] { "-telemetry" }));
        }

        [Test]
        public void TryGetValue_ReadsValueCaseInsensitively()
        {
            var args = new[] { "-BOT", "Pilot" };

            Assert.That(LaunchArguments.TryGetValue(args, "-bot", out var value), Is.True);
            Assert.That(value, Is.EqualTo("Pilot"));
        }

        [Test]
        public void TryGetValue_FlagFollowedByFlag_HasNoValue()
        {
            Assert.That(LaunchArguments.TryGetValue(new[] { "-bot", "-telemetry" }, "-bot", out _), Is.False);
            Assert.That(LaunchArguments.TryGetValue(new[] { "-bot" }, "-bot", out _), Is.False);
        }

        [Test]
        public void HasFlag_FindsFlag()
        {
            Assert.That(LaunchArguments.HasFlag(new[] { "a", "-Telemetry" }, "-telemetry"), Is.True);
            Assert.That(LaunchArguments.HasFlag(new[] { "a" }, "-telemetry"), Is.False);
        }
    }
}
