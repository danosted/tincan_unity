#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.UI;
using TinCan.Features.Voyage;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="VoyageEndScreenPresenter"/>: the end screen stays up while the voyage is over, and goes with a new voyage.</summary>
    public class VoyageEndScreenPresenterTests
    {
        private sealed class FakeState : IVoyageState
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public VoyagePhase Phase { get; set; }
            public int Voyage => 1;
            public Vector3 Destination => Vector3.zero;
            public int LayoutSeed => 0;
            public Vector3 Origin => Vector3.zero;
            public int BriefingSecondsLeft => 0;
            public void RequestRestart() { }
            public bool ConsumeRestartRequest() => false;
            public void ServerSetPhase(VoyagePhase phase) => Phase = phase;
            public void ServerSetVoyage(int voyage) { }
            public void ServerSetDestination(Vector3 destination) { }
            public void ServerSetLayout(int layoutSeed, Vector3 origin) { }
            public void ServerSetBriefingSecondsLeft(int seconds) { }
        }

        private VoyageConfig _config = null!;
        private MenuDefinition _main = null!;
        private MenuUseCase _menus = null!;
        private FakeState _state = null!;
        private VoyageEndScreenPresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<VoyageConfig>();
            _config.ArrivedMenu = MenuDefinition.Create("voyage_arrived", "Arrived");
            _config.LostMenu = MenuDefinition.Create("voyage_lost", "Lost");
            _main = MenuDefinition.Create("main", "TinCan");
            _menus = new MenuUseCase(new MenuCommandRegistry(Array.Empty<IMenuCommand>()));
            _state = new FakeState { Phase = VoyagePhase.Underway };
            var actors = new FakeActorRegistry();
            actors.Register(_state);
            _presenter = new VoyageEndScreenPresenter(actors, _menus, _config);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config.ArrivedMenu);
            UnityEngine.Object.DestroyImmediate(_config.LostMenu);
            UnityEngine.Object.DestroyImmediate(_main);
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void Underway_ShowsNothing()
        {
            _presenter.Tick();

            Assert.That(_menus.IsOpen, Is.False);
        }

        [Test]
        public void Ended_ShowsTheMatchingScreen_AsAPinnedModal_ThatBackCannotClose()
        {
            _state.Phase = VoyagePhase.Lost;
            _presenter.Tick();
            Assert.That(_menus.Current?.MenuId, Is.EqualTo("voyage_lost"));
            Assert.That((_menus.Current!.Kind, _menus.Current.Pinned), Is.EqualTo((MenuKind.Modal, true)));

            int changes = 0;
            _menus.Changed += () => changes++;
            Assert.That(_menus.Back(), Is.False, "Esc opens the pause menu over it instead");
            _menus.CloseAll();
            _presenter.Tick();

            Assert.That(_menus.Current?.MenuId, Is.EqualTo("voyage_lost"));
            Assert.That(changes, Is.Zero, "asked again every frame, changed never");
        }

        [Test]
        public void ANewVoyage_ClosesTheEndScreen()
        {
            _state.Phase = VoyagePhase.Arrived;
            _presenter.Tick();

            _state.Phase = VoyagePhase.Briefing;
            _presenter.Tick();

            Assert.That(_menus.IsOpen, Is.False);
        }

        [Test]
        public void TheEndScreen_CoversAnotherMenu_AndANewVoyageGivesItBack()
        {
            _menus.Open(_main);
            _state.Phase = VoyagePhase.Arrived;

            _presenter.Tick();

            Assert.That(_menus.Current?.MenuId, Is.EqualTo("voyage_arrived"));

            _state.Phase = VoyagePhase.Briefing;
            _presenter.Tick();
            Assert.That(_menus.Current?.MenuId, Is.EqualTo("main"), "a new voyage does not close the player's own menu");
        }
    }
}
