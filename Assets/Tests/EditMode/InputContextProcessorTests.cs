#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.Input;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    public class InputContextProcessorTests
    {
        private readonly InputContextProcessor _processor = new();
        private readonly List<InputContext> _live = new();
        private readonly HashSet<InputActionId> _enabled = new();

        private void Resolve(params InputContext[] contexts) => _processor.Resolve(contexts, c => c.Activation == InputContextActivation.Always, _live, _enabled);

        [Test]
        public void LiveContexts_AreOrderedByPriority_AndTurnOnTheirActions()
        {
            var jump = InputActionId.Create("Jump");
            var cancel = InputActionId.Create("Cancel");
            var low = FakeInputContexts.Plain("Low", InputContextActivation.Always, 0, actions: new[] { cancel });
            var high = FakeInputContexts.Plain("High", InputContextActivation.Always, 10, actions: new[] { jump });

            Resolve(low, high);

            Assert.That(_live, Is.EqualTo(new[] { high, low }));
            Assert.That(_enabled, Is.EquivalentTo(new[] { jump, cancel }));
        }

        [Test]
        public void ContextWhoseConditionFails_IsNotLive()
        {
            var jump = InputActionId.Create("Jump");
            var menu = FakeInputContexts.Plain("Menu", InputContextActivation.WhileMenuOpen, 10, actions: new[] { jump });

            Resolve(menu);

            Assert.That(_live, Is.Empty);
            Assert.That(_enabled, Is.Empty);
        }

        [Test]
        public void Blocks_SilenceTheNamedContexts_Only()
        {
            var walk = InputActionId.Create("Walk");
            var cancel = InputActionId.Create("Cancel");
            var aim = InputActionId.Create("Aim");
            var humanoid = FakeInputContexts.Plain("Humanoid", InputContextActivation.Always, 200, actions: new[] { walk });
            var global = FakeInputContexts.Plain("Global", InputContextActivation.Always, 0, actions: new[] { cancel });
            var gunner = FakeInputContexts.Plain("Gunner", InputContextActivation.Always, 400, actions: new[] { aim }, blocks: new[] { humanoid });

            Resolve(humanoid, global, gunner);

            Assert.That(_live, Is.EqualTo(new[] { gunner, global }));
            Assert.That(_enabled, Is.EquivalentTo(new[] { aim, cancel }));
        }

        [Test]
        public void BlocksAllLower_SilencesEverythingBelow_ButKeepsItsOwnRoutes()
        {
            var walk = InputActionId.Create("Walk");
            var cancel = InputActionId.Create("Cancel");
            var overlay = InputActionId.Create("Overlay");
            var back = UnityEngine.ScriptableObject.CreateInstance<TinCan.Core.UI.MenuBackCommand>();
            var humanoid = FakeInputContexts.Plain("Humanoid", InputContextActivation.Always, 200, actions: new[] { walk });
            var menu = FakeInputContexts.Plain("Menu", InputContextActivation.Always, 900, blocksAllLower: true, routes: new[] { new InputRoute(cancel, back) });
            var devTools = FakeInputContexts.Plain("DevTools", InputContextActivation.Always, 950, actions: new[] { overlay });

            Resolve(humanoid, menu, devTools);

            Assert.That(_live, Is.EqualTo(new[] { devTools, menu }));
            Assert.That(_enabled, Is.EquivalentTo(new[] { overlay, cancel }), "the menu's routed Cancel stays on");
        }

        [Test]
        public void BlockedContext_BlocksNothingItself()
        {
            var a = InputActionId.Create("A");
            var bottom = FakeInputContexts.Plain("Bottom", InputContextActivation.Always, 0, actions: new[] { a });
            var middle = FakeInputContexts.Plain("Middle", InputContextActivation.Always, 10, blocks: new[] { bottom });
            var top = FakeInputContexts.Plain("Top", InputContextActivation.Always, 20, blocks: new[] { middle });

            Resolve(bottom, middle, top);

            Assert.That(_live.Select(c => c.name), Is.EqualTo(new[] { "Top", "Bottom" }));
            Assert.That(_enabled, Does.Contain(a));
        }
    }
}
