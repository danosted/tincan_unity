#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Core.UI.Tests
{
    /// <summary>
    /// Menu requests: <see cref="MenuArbitrationProcessor"/> picks the one on top, <see cref="MenuUseCase"/> keeps one stack
    /// per request, and pinned requests stay until withdrawn. The conflicts from the UI plan (.docs/plans/ui-and-menus.md,
    /// "Today") each have a case here.
    /// </summary>
    public class MenuArbitrationProcessorTests
    {
        private MenuUseCase _menus = null!;
        private IInputCommandHandler _esc = null!;
        private MenuBackCommand _escCommand = null!;
        private MenuDefinition _main = null!;
        private MenuDefinition _join = null!;
        private MenuDefinition _overboard = null!;
        private MenuDefinition _lost = null!;
        private MenuDefinition _fittings = null!;

        [SetUp]
        public void SetUp()
        {
            _menus = new MenuUseCase(new MenuCommandRegistry(Array.Empty<IMenuCommand>()));
            _join = MenuDefinition.Create("join", "Join", new MenuItemDefinition { ItemId = "back", Label = "Back", Kind = MenuItemKind.Back });
            _main = MenuDefinition.Create("main", "Main", new MenuItemDefinition { ItemId = "join", Label = "Join", Kind = MenuItemKind.Submenu, Submenu = _join });
            _overboard = MenuDefinition.Create("overboard", "Overboard!");
            _lost = MenuDefinition.Create("lost", "Lost");
            _fittings = MenuDefinition.Create("fittings", "Mount a fitting", new MenuItemDefinition { ItemId = "back", Label = "Leave it empty", Kind = MenuItemKind.Back });
            _esc = new MenuBackInputHandler(_menus, _main);
            _escCommand = ScriptableObject.CreateInstance<MenuBackCommand>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in new Object[] { _main, _join, _overboard, _lost, _fittings, _escCommand }) Object.DestroyImmediate(asset);
        }

        [Test]
        public void Top_ModalBeatsScreenBeatsPanel_AndTheNewestWinsWithinAKind()
        {
            var ranks = new[]
            {
                new MenuRequestRank(MenuKind.Panel, 5),
                new MenuRequestRank(MenuKind.Modal, 1),
                new MenuRequestRank(MenuKind.Screen, 9),
                new MenuRequestRank(MenuKind.Modal, 3),
            };

            Assert.That(MenuArbitrationProcessor.Top(ranks), Is.EqualTo(3));
            Assert.That(MenuArbitrationProcessor.Top(Array.Empty<MenuRequestRank>()), Is.EqualTo(-1));
        }

        [Test]
        public void RequestingAgain_ChangesNothing_SoAUseCaseMayAskEveryFrame()
        {
            int changes = 0;
            _menus.Request(new MenuRequest("voyage", _lost, MenuKind.Modal, pinned: true));
            _menus.Changed += () => changes++;

            _menus.Request(new MenuRequest("voyage", _lost, MenuKind.Modal, pinned: true));

            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void EscWhileOverboard_OpensThePauseMenuOverTheChoice_AndClosingItReturnsToTheChoice()
        {
            // Was: the overboard menu pushed itself back on top every tick, so the pause menu could not be used.
            _menus.Request(new MenuRequest("overboard", _overboard, MenuKind.Modal, pinned: true));

            Assert.That(_esc.TryHandle(_escCommand), Is.True);
            Assert.That(_menus.Current!.MenuId, Is.EqualTo("main"));
            _menus.Request(new MenuRequest("overboard", _overboard, MenuKind.Modal, pinned: true)); // the use case's next tick
            Assert.That(_menus.Current!.MenuId, Is.EqualTo("main"), "the pause menu stays on top");

            Assert.That(_esc.TryHandle(_escCommand), Is.True);
            Assert.That(_menus.Current!.MenuId, Is.EqualTo("overboard"));
        }

        [Test]
        public void APinnedRequest_SurvivesBackAndCloseAll_AndGoesOnlyWhenWithdrawn()
        {
            _menus.Request(new MenuRequest("overboard", _overboard, MenuKind.Modal, pinned: true));

            Assert.That(_menus.Back(), Is.False);
            _menus.CloseAll();
            Assert.That(_menus.Current!.MenuId, Is.EqualTo("overboard"));

            _menus.Withdraw("overboard");
            Assert.That(_menus.IsOpen, Is.False);
        }

        [Test]
        public void LosingWhileOverboard_PutsTheEndScreenOnTop_AndTheChoiceIsStillThereAfterIt()
        {
            // Was: the end screen waited behind the overboard menu, then a CloseAll threw the rest away.
            _menus.Request(new MenuRequest("overboard", _overboard, MenuKind.Modal, pinned: true));
            _menus.Request(new MenuRequest("voyage", _lost, MenuKind.Modal, pinned: true));

            Assert.That(_menus.Current!.MenuId, Is.EqualTo("lost"));

            _menus.Withdraw("voyage"); // a new voyage
            Assert.That(_menus.Current!.MenuId, Is.EqualTo("overboard"));
        }

        [Test]
        public void TheFittingChooser_OverAnotherMenu_BacksOutToThatMenu_NotIntoIt()
        {
            // Was: the chooser pushed onto the other menu's stack, and Back walked into that menu's submenus.
            _menus.Request(new MenuRequest(MenuOwners.Main, _main, MenuKind.Screen));
            _menus.Invoke("join");
            _menus.Request(new MenuRequest("fittings", _fittings, MenuKind.Screen));

            Assert.That(_menus.Current!.MenuId, Is.EqualTo("fittings"));
            Assert.That(_menus.Current.CanGoBack, Is.False, "the chooser is its own request");

            _menus.Invoke("back");
            Assert.That(_menus.Current!.MenuId, Is.EqualTo("join"), "the other menu is as it was left");
            Assert.That(_menus.IsRequested("fittings"), Is.False);
        }

        [Test]
        public void BackInsideARequest_PopsItsSubmenu_ThenClosesIt()
        {
            _menus.Request(new MenuRequest(MenuOwners.Main, _main, MenuKind.Screen));
            _menus.Invoke("join");
            Assert.That(_menus.Current!.CanGoBack, Is.True);

            Assert.That(_menus.Back(), Is.True);
            Assert.That(_menus.Current!.MenuId, Is.EqualTo("main"));
            Assert.That(_menus.Back(), Is.True);
            Assert.That(_menus.IsOpen, Is.False);
        }

        [Test]
        public void ACommand_ClosesOnlyItsOwnRequest()
        {
            var close = new ClosingCommand();
            var menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[] { close }));
            var chooser = MenuDefinition.Create("fittings", "Mount", new MenuItemDefinition { ItemId = "cannon", Label = "Cannon", Kind = MenuItemKind.Command, CommandId = ClosingCommand.Id });
            menus.Request(new MenuRequest(MenuOwners.Main, _main, MenuKind.Screen));
            menus.Request(new MenuRequest("fittings", chooser, MenuKind.Screen));

            menus.Invoke("cannon");

            Assert.That(close.Owner, Is.EqualTo("fittings"));
            Assert.That(menus.Current!.MenuId, Is.EqualTo("main"));
            Object.DestroyImmediate(chooser);
        }

        [Test]
        public void TheSnapshot_CarriesTheRequest()
        {
            _menus.Request(new MenuRequest("voyage", _lost, MenuKind.Modal, pinned: true));

            Assert.That((_menus.Current!.Owner, _menus.Current.Kind, _menus.Current.Pinned), Is.EqualTo(("voyage", MenuKind.Modal, true)));
        }

        [Test]
        public void Backdrop_ScreenHidesTheHud_ModalKeepsIt_PanelChangesNothing()
        {
            Assert.That(MenuBackdropRules.For(null), Is.EqualTo(new MenuBackdrop(false, true)));
            Assert.That(MenuBackdropRules.For(MenuKind.Panel), Is.EqualTo(new MenuBackdrop(false, true)));
            Assert.That(MenuBackdropRules.For(MenuKind.Screen), Is.EqualTo(new MenuBackdrop(true, false)));
            Assert.That(MenuBackdropRules.For(MenuKind.Modal), Is.EqualTo(new MenuBackdrop(true, true)));
        }

        private sealed class ClosingCommand : IMenuCommand
        {
            public const string Id = "Close";
            public string? Owner { get; private set; }
            public string CommandId => Id;

            public void Execute(MenuContext context)
            {
                Owner = context.Owner;
                context.Close();
            }
        }
    }
}
