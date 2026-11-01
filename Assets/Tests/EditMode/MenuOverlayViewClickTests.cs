#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain.Settings;
using TinCan.Core.Settings;
using TinCan.Core.UI;
using TinCan.Core.UI.Components;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The menu overlay in a simulated panel (com.unity.ui.test-framework): real clicks on the drawn rows reach the model,
    /// the settings widgets write the player's settings, and a menu's backdrop keeps clicks from a panel underneath.
    /// </summary>
    public class MenuOverlayViewClickTests : UITestFixture
    {
        private GameObject _host = null!;
        private FakeUiLayers _layers = null!;
        private MenuUseCase _menus = null!;
        private FakeMenuCommand _command = null!;
        private PlayerSettingsUseCase _settings = null!;
        private SettingDefinition _sensitivity = null!;
        private SettingDefinition _quality = null!;
        private MenuDefinition _main = null!;
        private MenuDefinition _page = null!;

        [SetUp]
        public void SetUp()
        {
            panelSize = new Vector2(1000, 800);
            _sensitivity = SettingDefinition.Slider(LookSettings.SensitivityKey, "Look sensitivity", SettingGroup.Controls, 0.1f, 3f, 0.05f, 1f);
            _quality = SettingDefinition.Choice("graphics.quality", "Quality", SettingGroup.Controls, "Medium", "Low", "Medium", "High");
            _settings = new PlayerSettingsUseCase(new SettingsCatalog(new[] { _sensitivity, _quality }), new FakeSettingsStore(), new FakeTimeService());
            _command = new FakeMenuCommand("Host");
            _menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[] { _command }), null, _settings);
            _main = MenuDefinition.Create("main", "Main", new MenuItemDefinition { ItemId = "host", Label = "Start Host", Kind = MenuItemKind.Command, CommandId = "Host" });
            _page = MenuDefinition.Create("controls", "Controls", new MenuItemDefinition { ItemId = "settings", Kind = MenuItemKind.Settings, SettingsGroup = SettingGroup.Controls });

            var root = new VisualElement { style = { flexGrow = 1 } };
            root.style.position = Position.Absolute;
            root.style.left = root.style.top = root.style.right = root.style.bottom = 0;
            rootVisualElement.Add(root);
            _layers = new FakeUiLayers(root);

            // Named test assemblies cannot reference the predefined Assembly-CSharp assembly.
            _host = new GameObject("MenuOverlayClickTest");
            var view = _host.AddComponent(Type.GetType("TinCan.App.Views.MenuOverlayView, TinCan.App", true)!);
            view.GetType().GetMethod("Construct")!.Invoke(view, new object[] { _menus, _layers });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            foreach (var asset in new Object[] { _sensitivity, _quality, _main, _page }) Object.DestroyImmediate(asset);
        }

        private Button ButtonLabelled(string text) => rootVisualElement.Query<Button>().Where(b => b.text == text).First();

        [Test]
        public void ClickingARow_RunsItsCommand()
        {
            _menus.Open(_main);
            simulate.FrameUpdate();

            simulate.Click(ButtonLabelled("Start Host"));

            Assert.That(_command.Executions.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheChoiceArrows_StepThroughTheOptions()
        {
            _menus.Open(_page);
            simulate.FrameUpdate();

            simulate.Click(ButtonLabelled(">"));
            Assert.That(_settings.GetString("graphics.quality"), Is.EqualTo("High"));
            simulate.FrameUpdate();
            simulate.Click(ButtonLabelled(">"));
            simulate.Click(ButtonLabelled("<"));
            Assert.That(_settings.GetString("graphics.quality"), Is.EqualTo("High"), "wrapped to Low, then back");
            Assert.That(rootVisualElement.Q<ChoiceField>().Value, Is.EqualTo("High"), "the shown value follows");
        }

        [Test]
        public void TheSlider_WritesTheSetting_SnappedToItsStep()
        {
            _menus.Open(_page);
            simulate.FrameUpdate();

            rootVisualElement.Q<Slider>().value = 1.33f;

            Assert.That(_settings.GetFloat(LookSettings.SensitivityKey), Is.EqualTo(1.35f).Within(1e-4f));
            Assert.That(rootVisualElement.Q<Slider>().value, Is.EqualTo(1.35f).Within(1e-4f), "the slider shows the kept value");
        }

        [Test]
        public void TheFocusedRow_IsMarked_AndAClickedRowTakesTheFocus()
        {
            _menus.Open(_page);
            simulate.FrameUpdate();
            var rows = rootVisualElement.Q<MenuPanel>().Rows;
            Assert.That(rows[0].ClassListContains(MenuPanel.FocusedRowUssClassName), Is.True, "the slider, the first row");

            simulate.Click(ButtonLabelled(">"));
            simulate.FrameUpdate();

            Assert.That(rows[0].ClassListContains(MenuPanel.FocusedRowUssClassName), Is.False);
            Assert.That(rows[1].ClassListContains(MenuPanel.FocusedRowUssClassName), Is.True, "the choice that was clicked");
        }

        [Test]
        public void TheModelsFocus_IsUiToolkitsFocusToo()
        {
            _menus.Open(_main);
            simulate.FrameUpdate();

            Assert.That(panel.focusController.focusedElement, Is.SameAs(ButtonLabelled("Start Host")), "the border and the fill are one row");
        }

        [Test]
        public void UiToolkitsOwnNavigation_NeitherMovesTheFocusNorClicks()
        {
            var two = MenuDefinition.Create("two", "Two",
                new MenuItemDefinition { ItemId = "host", Label = "Start Host", Kind = MenuItemKind.Command, CommandId = "Host" },
                new MenuItemDefinition { ItemId = "again", Label = "Host Again", Kind = MenuItemKind.Command, CommandId = "Host" });
            UiLayers.StopBuiltInNavigation(panel.visualTree);
            _menus.Open(two);
            simulate.FrameUpdate();
            var focused = panel.focusController.focusedElement;

            simulate.KeyPress(KeyCode.DownArrow);
            simulate.KeyPress(KeyCode.S);
            simulate.KeyPress(KeyCode.Tab);
            simulate.FrameUpdate();
            simulate.ReturnKeyPress();
            simulate.FrameUpdate();

            Assert.That(panel.focusController.focusedElement, Is.SameAs(focused), "only the Menu context moves the focus");
            Assert.That(_command.Executions.Count, Is.Zero, "only the Menu context's Submit clicks");
            Object.DestroyImmediate(two);
        }

        [Test]
        public void AMenusBackdrop_KeepsClicksFromAPanelUnderneath()
        {
            int panelClicks = 0;
            var panelButton = new Button(() => panelClicks++) { text = "Panel tile" };
            panelButton.style.position = Position.Absolute;
            panelButton.style.left = 10;
            panelButton.style.top = 10;
            panelButton.style.width = 120;
            panelButton.style.height = 40;
            _layers.Root(UiLayer.Panels).Add(panelButton);
            simulate.FrameUpdate();

            simulate.Click(panelButton);
            Assert.That(panelClicks, Is.EqualTo(1), "nothing open: the panel takes the click");

            _menus.Open(_main);
            simulate.FrameUpdate();
            simulate.Click(panelButton.worldBound.center);
            Assert.That(panelClicks, Is.EqualTo(1), "under a Screen menu the backdrop takes it");
        }
    }
}
