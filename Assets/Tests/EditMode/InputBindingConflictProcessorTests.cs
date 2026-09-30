#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.Input;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    public class InputBindingConflictProcessorTests
    {
        private readonly InputBindingConflictProcessor _conflicts = new();

        private static InputContext Possessing(string name, PossessedActorKind kind, int priority, params InputActionId[] actions)
        {
            var context = FakeInputContexts.Plain(name, InputContextActivation.WhilePossessing, priority, actions: actions);
            context.Configure(InputContextActivation.WhilePossessing, priority, kind);
            return context;
        }

        [Test]
        public void SameContext_CannotShare()
        {
            var jump = InputActionId.Create("Jump");
            var sprint = InputActionId.Create("Sprint");
            var humanoid = Possessing("Humanoid", PossessedActorKind.Humanoid, 200, jump, sprint);

            Assert.That(_conflicts.CanShareAKey(jump, sprint, new[] { humanoid }), Is.False);
        }

        [Test]
        public void DifferentPossessedKinds_CanShare()
        {
            var jump = InputActionId.Create("Jump");
            var pitch = InputActionId.Create("Pitch");
            var contexts = new[] { Possessing("Humanoid", PossessedActorKind.Humanoid, 200, jump), Possessing("Airship", PossessedActorKind.Ship, 300, pitch) };

            Assert.That(_conflicts.CanShareAKey(jump, pitch, contexts), Is.True);
        }

        [Test]
        public void BlockedContexts_CanShare_UnblockedCannot()
        {
            var primary = InputActionId.Create("Primary");
            var fire = InputActionId.Create("Fire");
            var cancel = InputActionId.Create("Cancel");
            var humanoid = Possessing("Humanoid", PossessedActorKind.Humanoid, 200, primary);
            var gunner = FakeInputContexts.Plain("Gunner", InputContextActivation.WhilePossessedHasTag, 400, actions: new[] { fire }, blocks: new[] { humanoid });
            var global = FakeInputContexts.Plain("Global", InputContextActivation.Always, 0, actions: new[] { cancel });
            var contexts = new[] { humanoid, gunner, global };

            Assert.That(_conflicts.CanShareAKey(primary, fire, contexts), Is.True, "the gunner blocks walking");
            Assert.That(_conflicts.CanShareAKey(fire, cancel, contexts), Is.False, "Global is live under the gunner");
        }

        [Test]
        public void BlocksAllLower_SeparatesItFromEverythingBelow()
        {
            var back = InputActionId.Create("Back");
            var jump = InputActionId.Create("Jump");
            var menu = FakeInputContexts.Plain("Menu", InputContextActivation.WhileMenuOpen, 900, blocksAllLower: true, actions: new[] { back });
            var humanoid = Possessing("Humanoid", PossessedActorKind.Humanoid, 200, jump);

            Assert.That(_conflicts.CanShareAKey(back, jump, new[] { menu, humanoid }), Is.True);
        }
    }
}
