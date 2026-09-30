#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.Input;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    public class InputContextUseCaseTests
    {
        private sealed class SwitchableConditions : IInputContextConditions
        {
            public readonly HashSet<InputContext> On = new();
            public bool Holds(InputContext context) => On.Contains(context);
        }

        private sealed class RecordingSwitch : IInputActionSwitch
        {
            public readonly List<HashSet<InputActionId>> Calls = new();
            public void SetEnabled(IReadOnlyCollection<InputActionId> enabled) => Calls.Add(new HashSet<InputActionId>(enabled));
        }

        private InputActionId _walk = null!;
        private InputActionId _cancel = null!;
        private InputContext _humanoid = null!;
        private InputContext _menu = null!;
        private SwitchableConditions _conditions = null!;
        private RecordingSwitch _switch = null!;
        private InputContextUseCase _useCase = null!;

        [SetUp]
        public void SetUp()
        {
            _walk = InputActionId.Create("Walk");
            _cancel = InputActionId.Create("Cancel");
            _humanoid = FakeInputContexts.Plain("Humanoid", InputContextActivation.WhilePossessing, 200, actions: new[] { _walk });
            _menu = FakeInputContexts.Plain("Menu", InputContextActivation.WhileMenuOpen, 900, blocksAllLower: true, actions: new[] { _cancel });
            _conditions = new SwitchableConditions();
            _switch = new RecordingSwitch();
            _useCase = new InputContextUseCase(new InputContextSet(new[] { _humanoid, _menu }), _conditions, _switch, new InputContextProcessor());
        }

        [Test]
        public void Initialize_EnablesTheLiveContextsActions()
        {
            _conditions.On.Add(_humanoid);

            _useCase.Initialize();

            Assert.That(_useCase.Active, Is.EqualTo(new[] { _humanoid }));
            Assert.That(_useCase.IsActive(_humanoid), Is.True);
            Assert.That(_switch.Calls[^1], Is.EquivalentTo(new[] { _walk }));
        }

        [Test]
        public void MenuOpens_SilencesGameplay_AndRaisesChanged_ThenRestores()
        {
            _conditions.On.Add(_humanoid);
            _useCase.Initialize();
            int changes = 0;
            _useCase.Changed += () => changes++;

            _conditions.On.Add(_menu);
            _useCase.Tick();

            Assert.That(_useCase.Active, Is.EqualTo(new[] { _menu }));
            Assert.That(_switch.Calls[^1], Is.EquivalentTo(new[] { _cancel }));
            Assert.That(changes, Is.EqualTo(1));

            _conditions.On.Remove(_menu);
            _useCase.Tick();

            Assert.That(_useCase.Active, Is.EqualTo(new[] { _humanoid }));
            Assert.That(_switch.Calls[^1], Is.EquivalentTo(new[] { _walk }));
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void NothingChanged_DoesNotTouchTheActionsAgain()
        {
            _conditions.On.Add(_humanoid);
            _useCase.Initialize();
            int calls = _switch.Calls.Count;

            _useCase.Tick();
            _useCase.Tick();

            Assert.That(_switch.Calls.Count, Is.EqualTo(calls));
        }
    }
}
