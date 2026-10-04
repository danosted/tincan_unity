#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Input;
using TinCan.Core.Input;
using TinCan.Core.UI;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class InputContextConditionsTests
    {
        private FakePossessionState _possession = null!;
        private MenuUseCase _menus = null!;
        private InputRebindState _rebinding = null!;
        private InputContextConditions _conditions = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeHumanoidCharacterView _body = null!;
        private FakePossessable _freeCamera = null!;
        private GameplayTag _occupying = null!;
        private MenuDefinition _menu = null!;

        [SetUp]
        public void SetUp()
        {
            _possession = new FakePossessionState();
            _menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[0]));
            _rebinding = new InputRebindState();
            _conditions = new InputContextConditions(_possession, _menus, _rebinding);
            _movement = new FakeHumanoidMovementView("Body");
            _body = new FakeHumanoidCharacterView(_movement);
            _freeCamera = new FakePossessable();
            _occupying = ScriptableObject.CreateInstance<GameplayTag>();
            _menu = MenuDefinition.Create("m", "M");
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
            Object.DestroyImmediate(_occupying);
            Object.DestroyImmediate(_menu);
        }

        private static InputContext Possessing(PossessedActorKind kind)
        {
            var context = ScriptableObject.CreateInstance<InputContext>();
            context.Configure(InputContextActivation.WhilePossessing, 0, kind);
            return context;
        }

        [Test]
        public void WhilePossessing_FollowsTheKindOfWhatThePlayerControls()
        {
            var humanoid = Possessing(PossessedActorKind.Humanoid);
            var other = Possessing(PossessedActorKind.Other);

            _possession.CurrentPossession = _body;
            Assert.That(_conditions.Holds(humanoid), Is.True);
            Assert.That(_conditions.Holds(other), Is.False);

            _possession.CurrentPossession = _freeCamera;
            Assert.That(_conditions.Holds(humanoid), Is.False);
            Assert.That(_conditions.Holds(other), Is.True);

            _possession.CurrentPossession = null;
            Assert.That(_conditions.Holds(humanoid), Is.False, "offline nothing is possessed");
        }

        [Test]
        public void WhilePossessedHasTag_FollowsTheReplicatedTag()
        {
            var gunner = ScriptableObject.CreateInstance<InputContext>();
            gunner.Configure(InputContextActivation.WhilePossessedHasTag, 0, tag: _occupying);
            _possession.CurrentPossession = _body;

            Assert.That(_conditions.Holds(gunner), Is.False);

            _body.HeldTags.Add(_occupying);
            Assert.That(_conditions.Holds(gunner), Is.True);
        }

        [Test]
        public void MenuAndRebinding_FollowTheirState()
        {
            var menu = ScriptableObject.CreateInstance<InputContext>();
            menu.Configure(InputContextActivation.WhileMenuOpen, 0);
            var rebinding = ScriptableObject.CreateInstance<InputContext>();
            rebinding.Configure(InputContextActivation.WhileRebinding, 0);
            var always = ScriptableObject.CreateInstance<InputContext>();
            always.Configure(InputContextActivation.Always, 0);

            Assert.That(_conditions.Holds(menu), Is.False);
            Assert.That(_conditions.Holds(rebinding), Is.False);
            Assert.That(_conditions.Holds(always), Is.True);

            _menus.Open(_menu);
            _rebinding.IsRebinding = true;

            Assert.That(_conditions.Holds(menu), Is.True);
            Assert.That(_conditions.Holds(rebinding), Is.True);
        }

        [Test]
        public void WhileOpened_HoldsBetweenItsOwnerOpeningAndClosingIt()
        {
            var contextSwitch = new InputContextSwitch();
            var conditions = new InputContextConditions(_possession, _menus, _rebinding, contextSwitch);
            var mode = ScriptableObject.CreateInstance<InputContext>();
            var other = ScriptableObject.CreateInstance<InputContext>();
            mode.Configure(InputContextActivation.WhileOpened, 0);
            other.Configure(InputContextActivation.WhileOpened, 0);
            try
            {
                Assert.That(conditions.Holds(mode), Is.False);

                contextSwitch.SetOpen(mode, true);
                Assert.That(conditions.Holds(mode), Is.True);
                Assert.That(conditions.Holds(other), Is.False);

                contextSwitch.SetOpen(mode, false);
                Assert.That(conditions.Holds(mode), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(mode);
                Object.DestroyImmediate(other);
            }
        }
    }
}
