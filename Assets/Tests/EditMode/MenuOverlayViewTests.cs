#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.UI;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The throwaway overlays drawn into the layers: text editing survives re-renders, a modal draws on the Modal layer
    /// over a dimmed scene, and the HUD follows <see cref="MenuBackdropRules"/>.
    /// </summary>
    public class MenuOverlayViewTests
    {
        private GameObject _object = null!;
        private PanelSettings _settings = null!;
        private UIDocument _document = null!;
        private FakeUiLayers _layers = null!;
        private MenuDefinition _menu = null!;
        private MenuDefinition _modal = null!;
        private MenuUseCase _menus = null!;
        private FakeHudValues _hud = null!;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _object = new GameObject("MenuOverlayTest");
            _document = _object.AddComponent<UIDocument>();
            _document.panelSettings = _settings;
            _layers = new FakeUiLayers(_document.rootVisualElement);
            _menus = new MenuUseCase(new MenuCommandRegistry(Array.Empty<IMenuCommand>()));
            _hud = new FakeHudValues();
            _menu = MenuDefinition.Create("join", "Join",
                new MenuItemDefinition { ItemId = "address", Label = "Address", Kind = MenuItemKind.TextField, DefaultValue = "" });
            _modal = MenuDefinition.Create("lost", "Lost", new MenuItemDefinition { ItemId = "restart", Label = "Restart", Kind = MenuItemKind.Command });

            // Named test assemblies cannot reference the predefined Assembly-CSharp assembly.
            var menuView = _object.AddComponent(Type.GetType("TinCan.App.Views.MenuOverlayView, TinCan.App", true)!);
            menuView.GetType().GetMethod("Construct")!.Invoke(menuView, new object[] { _menus, _layers });
            var hudView = _object.AddComponent(Type.GetType("TinCan.App.Views.HudOverlayView, TinCan.App", true)!);
            hudView.GetType().GetMethod("Construct")!.Invoke(hudView, new object[] { _hud, _menus, _layers });
            _menus.Open(_menu);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_menu);
            Object.DestroyImmediate(_modal);
        }

        [Test]
        public void Typing_PreservesTheFieldAndStoresEveryEdit()
        {
            var field = _document.rootVisualElement.Q<TextField>();
            Assert.That(field, Is.Not.Null);
            foreach (char character in "192.168.1.42")
            {
                field.value += character;
                Assert.That(_document.rootVisualElement.Q<TextField>(), Is.SameAs(field));
                Assert.That(_menus.GetValue("address"), Is.EqualTo(field.value));
            }
        }

        [Test]
        public void ProgrammaticUpdate_RefreshesExistingFieldWithoutAnotherChangeEvent()
        {
            var field = _document.rootVisualElement.Q<TextField>();
            int changes = 0;
            field.RegisterValueChangedCallback(_ => changes++);

            _menus.SetValue("address", "10.0.0.7");

            Assert.That(_document.rootVisualElement.Q<TextField>(), Is.SameAs(field));
            Assert.That(field.value, Is.EqualTo("10.0.0.7"));
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void AScreen_DrawsOnTheMenusLayer_OverADimmedScene()
        {
            var menus = _layers.Root(UiLayer.Menus);

            Assert.That(menus.Q<TextField>(), Is.Not.Null);
            Assert.That(menus.Q("menu-backdrop").style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void AModal_MovesToTheModalLayer_AndBackWhenItGoes()
        {
            _menus.Request(new MenuRequest("voyage", _modal, MenuKind.Modal, pinned: true));

            Assert.That(_layers.Root(UiLayer.Modal).Q<Button>()?.text, Is.EqualTo("Restart"));
            Assert.That(_layers.Root(UiLayer.Menus).childCount, Is.Zero);

            _menus.Withdraw("voyage");
            Assert.That(_layers.Root(UiLayer.Menus).Q<TextField>(), Is.Not.Null, "the screen underneath is back");
        }

        [Test]
        public void TheHud_HidesUnderAScreen_AndShowsUnderAModal()
        {
            _hud.Set("Fuel", "40%");
            var card = _layers.Root(UiLayer.Hud).ElementAt(0);
            Assert.That(card.style.display.value, Is.EqualTo(DisplayStyle.None), "the join screen is up");

            _menus.Request(new MenuRequest("voyage", _modal, MenuKind.Modal, pinned: true));
            Assert.That(card.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            _menus.Withdraw("voyage");
            _menus.CloseAll();
            Assert.That(card.style.display.value, Is.EqualTo(DisplayStyle.Flex), "nothing open");
        }
    }
}
