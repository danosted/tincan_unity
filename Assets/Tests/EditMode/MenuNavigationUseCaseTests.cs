#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Settings;
using TinCan.Core.Settings;
using TinCan.Core.UI;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Keyboard and gamepad in menus: <see cref="MenuFocusProcessor"/> (which rows take focus, what left and right do),
    /// the focus in <see cref="MenuUseCase"/>, and <see cref="MenuNavigationUseCase"/> reading the Menu context with key
    /// repeat.
    /// </summary>
    public class MenuNavigationUseCaseTests
    {
        private FakeMenuCommand _host = null!;
        private MenuUseCase _menus = null!;
        private MenuDefinition _join = null!;
        private MenuDefinition _main = null!;
        private SettingDefinition _sensitivity = null!;
        private SettingDefinition _quality = null!;
        private PlayerSettingsUseCase _settings = null!;
        private MenuDefinition _page = null!;
        private MenuInputContext _controls = null!;
        private InputActionId _navigate = null!;
        private InputActionId _submit = null!;
        private InputActionId _letters = null!;
        private FakeInputReader _input = null!;
        private FakeTimeService _time = null!;
        private MenuNavigationUseCase _navigation = null!;

        [SetUp]
        public void SetUp()
        {
            _sensitivity = SettingDefinition.Slider(LookSettings.SensitivityKey, "Look sensitivity", SettingGroup.Controls, 0.1f, 3f, 0.05f, 1f);
            _quality = SettingDefinition.Choice("graphics.quality", "Quality", SettingGroup.Controls, "Medium", "Low", "Medium", "High");
            _settings = new PlayerSettingsUseCase(new SettingsCatalog(new[] { _sensitivity, _quality }), new FakeSettingsStore(), new FakeTimeService());
            _host = new FakeMenuCommand("Host");
            _menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[] { _host }), null, _settings);
            _join = MenuDefinition.Create("join", "Join", new MenuItemDefinition { ItemId = "address", Label = "Address", Kind = MenuItemKind.TextField });
            _main = MenuDefinition.Create("main", "Main",
                new MenuItemDefinition { ItemId = "host", Label = "Start Host", Kind = MenuItemKind.Command, CommandId = "Host" },
                new MenuItemDefinition { ItemId = "join", Label = "Join", Kind = MenuItemKind.Submenu, Submenu = _join },
                new MenuItemDefinition { ItemId = "quit", Label = "Quit", Kind = MenuItemKind.Command, CommandId = "Quit" });
            _page = MenuDefinition.Create("controls", "Controls",
                new MenuItemDefinition { ItemId = "note", Label = "Settings", Kind = MenuItemKind.Note },
                new MenuItemDefinition { ItemId = "settings", Kind = MenuItemKind.Settings, SettingsGroup = SettingGroup.Controls });

            _navigate = InputActionId.Create("Menu/Navigate");
            _submit = InputActionId.Create("Menu/Submit");
            _letters = InputActionId.Create("Menu/NavigateLetters");
            _controls = ScriptableObject.CreateInstance<MenuInputContext>();
            _controls.Navigate = _navigate;
            _controls.Submit = _submit;
            _controls.NavigateLetters = _letters;
            _input = new FakeInputReader();
            _time = new FakeTimeService { Time = 1f };
            _navigation = new MenuNavigationUseCase(_input, _controls, _menus, _time);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in new Object[] { _join, _main, _page, _sensitivity, _quality, _controls, _navigate, _submit, _letters }) Object.DestroyImmediate(asset);
        }

        private void Hold(Vector2 direction, float seconds = 0f)
        {
            _input.Vectors[_navigate] = direction;
            _time.Time += seconds;
            _navigation.Tick();
        }

        private void Release()
        {
            _input.Vectors.Remove(_navigate);
            _navigation.Tick();
        }

        private string FocusedId => _menus.Current!.Items[_menus.Current.Focused].ItemId;

        [Test]
        public void AMenu_OpensWithItsFirstUsableRowFocused_SkippingNotes()
        {
            _menus.Open(_page);

            Assert.That(FocusedId, Is.EqualTo("settings/look.sensitivity"), "the note above it is not focusable");
        }

        [Test]
        public void UpAndDown_MoveTheFocus_AndWrap()
        {
            _menus.Open(_main);

            Hold(Vector2.down);
            Assert.That(FocusedId, Is.EqualTo("join"));
            Release();
            Hold(Vector2.up);
            Release();
            Hold(Vector2.up);
            Assert.That(FocusedId, Is.EqualTo("quit"), "up from the first row wraps to the last");
        }

        [Test]
        public void AHeldDirection_RepeatsAfterADelay()
        {
            _menus.Open(_main);

            Hold(Vector2.down);
            Hold(Vector2.down, MenuNavigationUseCase.RepeatDelaySeconds * 0.5f);
            Assert.That(FocusedId, Is.EqualTo("join"), "no repeat before the delay");

            Hold(Vector2.down, MenuNavigationUseCase.RepeatDelaySeconds * 0.6f);
            Assert.That(FocusedId, Is.EqualTo("quit"));
            Hold(Vector2.down, MenuNavigationUseCase.RepeatIntervalSeconds * 1.1f);
            Assert.That(FocusedId, Is.EqualTo("host"), "then every interval");
        }

        [Test]
        public void Submit_UsesTheFocusedRow()
        {
            _menus.Open(_main);
            Hold(Vector2.down);
            Release();

            _input.Triggered.Add(_submit);
            _navigation.Tick();

            Assert.That(_menus.Current!.MenuId, Is.EqualTo("join"), "the Join submenu opened");
            Assert.That(FocusedId, Is.EqualTo("address"), "a new menu focuses its first row");
        }

        [Test]
        public void LeftAndRight_StepASlider_AndAChoice()
        {
            _menus.Open(_page);

            Hold(Vector2.right);
            Assert.That(_settings.GetFloat(LookSettings.SensitivityKey), Is.EqualTo(1.05f).Within(1e-4f));
            Release();

            Hold(Vector2.down);
            Release();
            Hold(Vector2.left);
            Assert.That(_settings.GetString("graphics.quality"), Is.EqualTo("Low"));
            Assert.That(FocusedId, Is.EqualTo("settings/graphics.quality"), "the focus stays on the row as its value changes");
        }

        [Test]
        public void TheLetters_Navigate_ExceptOnATextField_WhereTheArrowsStillDo()
        {
            _menus.Open(_main);
            _input.Vectors[_letters] = Vector2.down;
            _navigation.Tick();
            Assert.That(FocusedId, Is.EqualTo("join"), "S moves down on a button");
            _input.Vectors.Remove(_letters);
            _navigation.Tick();

            _menus.Invoke("join");
            Assert.That(FocusedId, Is.EqualTo("address"));
            _input.Vectors[_letters] = Vector2.down;
            _navigation.Tick();
            Assert.That(FocusedId, Is.EqualTo("address"), "on a text field S is a letter");
            _input.Vectors.Remove(_letters);

            var withButton = MenuDefinition.Create("join2", "Join",
                new MenuItemDefinition { ItemId = "address", Label = "Address", Kind = MenuItemKind.TextField },
                new MenuItemDefinition { ItemId = "connect", Label = "Connect", Kind = MenuItemKind.Command, CommandId = "Host" });
            _menus.Open(withButton);
            Hold(Vector2.down);
            Assert.That(FocusedId, Is.EqualTo("connect"), "the arrows leave a text field");
            Object.DestroyImmediate(withButton);
        }

        [Test]
        public void TheFocus_FollowsAClickedRow()
        {
            _menus.Open(_main);

            _menus.Focus("quit");

            Assert.That(FocusedId, Is.EqualTo("quit"));
        }

        [Test]
        public void Direction_TakesTheStrongerAxis_PastADeadZone()
        {
            Assert.That(MenuNavigationUseCase.Direction(new Vector2(0.2f, 0.3f)), Is.EqualTo(Vector2Int.zero));
            Assert.That(MenuNavigationUseCase.Direction(new Vector2(0.6f, 0.9f)), Is.EqualTo(Vector2Int.up));
            Assert.That(MenuNavigationUseCase.Direction(new Vector2(-0.9f, 0.6f)), Is.EqualTo(Vector2Int.left));
        }

        [Test]
        public void Adjust_ChangesOnlyValueRows()
        {
            var slider = new MenuItemRow("s", "S", MenuItemKind.Slider, "0", 0f, 10f, 0f, Array.Empty<string>());
            var toggle = new MenuItemRow("t", "T", MenuItemKind.Toggle, bool.FalseString);
            var button = new MenuItemRow("b", "B", MenuItemKind.Command, string.Empty);

            Assert.That(MenuFocusProcessor.Adjust(slider, 1), Is.EqualTo("0.5"), "a twentieth of the range without a step");
            Assert.That(MenuFocusProcessor.Adjust(slider, -1), Is.EqualTo("0"), "clamped");
            Assert.That(MenuFocusProcessor.Adjust(toggle, 1), Is.EqualTo(bool.TrueString));
            Assert.That(MenuFocusProcessor.Adjust(button, 1), Is.Null);
        }
    }
}
