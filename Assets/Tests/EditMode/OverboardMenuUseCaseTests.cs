#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.UI;
using TinCan.Features.Respawn;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class OverboardMenuUseCaseTests
    {
        private RespawnRules _rules = null!;
        private GameplayTag _overboardTag = null!;
        private FakeActorRegistry _actors = null!;
        private FakeMenus _menus = null!;
        private FakeHudValues _hud = null!;
        private FakeTimeService _time = null!;
        private OverboardChoice _choice = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeHumanoidCharacterView _player = null!;
        private OverboardMenuUseCase _useCase = null!;

        [SetUp]
        public void SetUp()
        {
            _overboardTag = ScriptableObject.CreateInstance<GameplayTag>();
            _rules = ScriptableObject.CreateInstance<RespawnRules>();
            _rules.OverboardTag = _overboardTag;
            _rules.Rules = new List<RespawnRule> { new() { Cause = RespawnCause.Overboard, DelaySeconds = 0f } };
            _actors = new FakeActorRegistry();
            _menus = new FakeMenus();
            _hud = new FakeHudValues();
            _time = new FakeTimeService { Time = 10f };
            _choice = new OverboardChoice();
            _movement = new FakeHumanoidMovementView("Player");
            _player = new FakeHumanoidCharacterView(_movement);
            _actors.Register(_player);
            _actors.LocalPlayer = _player;
            _useCase = new OverboardMenuUseCase(_rules, _actors, _menus, _hud, _time, _choice);
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
            Object.DestroyImmediate(_rules);
            Object.DestroyImmediate(_overboardTag);
        }

        [Test]
        public void Overboard_OpensTheChoice_AndShowsTheHudLine()
        {
            _player.HeldTags.Add(_overboardTag);

            _useCase.Tick();

            Assert.That(_menus.Current?.MenuId, Is.EqualTo(OverboardMenuUseCase.MenuId));
            Assert.That(_hud.All[OverboardMenuUseCase.HudKey], Is.EqualTo("Overboard"));
        }

        [Test]
        public void ClosingTheChoiceUnchosen_ReopensIt()
        {
            _player.HeldTags.Add(_overboardTag);
            _useCase.Tick();

            _menus.CloseAll();
            _useCase.Tick();

            Assert.That(_menus.Current?.MenuId, Is.EqualTo(OverboardMenuUseCase.MenuId), "Nobody is stranded overboard.");
        }

        [Test]
        public void AfterChoosing_ItStaysClosed_AndClearsWhenAboard()
        {
            _player.HeldTags.Add(_overboardTag);
            _useCase.Tick();
            _choice.Choose(OverboardChoiceKind.Respawn, _time.Time);
            _menus.CloseAll();

            _useCase.Tick();
            Assert.That(_menus.IsOpen, Is.False);
            Assert.That(_hud.All[OverboardMenuUseCase.HudKey], Is.EqualTo("Respawning"));

            _player.HeldTags.Clear();
            _useCase.Tick();
            Assert.That(_hud.All.ContainsKey(OverboardMenuUseCase.HudKey), Is.False);
        }

        [Test]
        public void ADelay_CountsDownOnTheHud()
        {
            _rules.Rules[0].DelaySeconds = 5f;
            _player.HeldTags.Add(_overboardTag);
            _useCase.Tick();

            _time.Time += 2.2f;
            _useCase.Tick();

            Assert.That(_hud.All[OverboardMenuUseCase.HudKey], Is.EqualTo("Overboard: respawn in 3 s"));
        }

        [Test]
        public void NotOverboard_ShowsNothing()
        {
            _useCase.Tick();

            Assert.That(_menus.IsOpen, Is.False);
            Assert.That(_hud.All.ContainsKey(OverboardMenuUseCase.HudKey), Is.False);
        }

        private sealed class FakeMenus : IMenuSystem
        {
            private readonly List<MenuDefinition> _stack = new();

            public MenuSnapshot? Current => _stack.Count == 0
                ? null
                : new MenuSnapshot(_stack[^1].MenuId, _stack[^1].Title, Array.Empty<MenuItemRow>(), _stack.Count > 1);
            public bool IsOpen => _stack.Count > 0;
            public event Action? Changed;

            public void Open(MenuDefinition menu)
            {
                _stack.Add(menu);
                Changed?.Invoke();
            }

            public void Back() => _stack.RemoveAt(_stack.Count - 1);
            public void CloseAll() => _stack.Clear();
            public void Invoke(string itemId) { }
            public void SetValue(string itemId, string value) { }
            public string GetValue(string itemId) => string.Empty;
        }
    }
}
