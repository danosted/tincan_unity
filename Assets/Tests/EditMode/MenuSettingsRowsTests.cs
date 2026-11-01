#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Settings;
using TinCan.Core.Settings;
using TinCan.Core.UI;
using TinCan.Tests.EditMode.Fakes;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// The Settings pages inside <see cref="MenuUseCase"/> (<see cref="MenuSettingsRows"/>): a Settings row becomes one row
    /// per setting of its group, edits go to <see cref="IPlayerSettings"/>, and Reset resets the page.
    /// </summary>
    public class MenuSettingsRowsTests
    {
        private SettingDefinition _sensitivity = null!;
        private SettingDefinition _invert = null!;
        private SettingDefinition _quality = null!;
        private PlayerSettingsUseCase _settings = null!;
        private MenuUseCase _menus = null!;
        private MenuDefinition _controls = null!;
        private MenuDefinition _graphics = null!;
        private MenuDefinition _audio = null!;

        [SetUp]
        public void SetUp()
        {
            _sensitivity = SettingDefinition.Slider(LookSettings.SensitivityKey, "Look sensitivity", SettingGroup.Controls, 0.1f, 3f, 0.05f, 1f);
            _invert = SettingDefinition.Toggle(LookSettings.InvertYKey, "Invert Y", SettingGroup.Controls, false);
            _quality = SettingDefinition.Choice("graphics.quality", "Quality", SettingGroup.Graphics, "Medium", "Low", "Medium", "High");
            _settings = new PlayerSettingsUseCase(new SettingsCatalog(new[] { _sensitivity, _invert, _quality }), new FakeSettingsStore(), new FakeTimeService());
            _menus = new MenuUseCase(new MenuCommandRegistry(Array.Empty<IMenuCommand>()), null, _settings);
            _controls = Page("controls", SettingGroup.Controls);
            _graphics = Page("graphics", SettingGroup.Graphics);
            _audio = Page("audio", SettingGroup.Audio);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in new Object[] { _sensitivity, _invert, _quality, _controls, _graphics, _audio }) Object.DestroyImmediate(asset);
        }

        private static MenuDefinition Page(string id, SettingGroup group) => MenuDefinition.Create(id, id,
            new MenuItemDefinition { ItemId = "settings", Label = group.ToString(), Kind = MenuItemKind.Settings, SettingsGroup = group },
            new MenuItemDefinition { ItemId = "back", Label = "Back", Kind = MenuItemKind.Back });

        [Test]
        public void ASettingsRow_ExpandsToItsGroup_ThenReset()
        {
            _menus.Open(_controls);

            var rows = _menus.Current!.Items;
            Assert.That(rows.Select(r => (r.ItemId, r.Kind)), Is.EqualTo(new[]
            {
                ("settings/look.sensitivity", MenuItemKind.Slider),
                ("settings/look.invert_y", MenuItemKind.Toggle),
                ("settings/reset", MenuItemKind.Command),
                ("back", MenuItemKind.Back),
            }));
            Assert.That((rows[0].Value, rows[0].Min, rows[0].Max, rows[0].Step), Is.EqualTo(("1", 0.1f, 3f, 0.05f)));
        }

        [Test]
        public void ASlider_WritesThePlayersSetting_Clamped()
        {
            _menus.Open(_controls);

            _menus.SetValue("settings/look.sensitivity", "7");

            Assert.That(_settings.GetFloat(LookSettings.SensitivityKey), Is.EqualTo(3f));
            Assert.That(_menus.Current!.Items[0].Value, Is.EqualTo("3"), "the row shows the kept value");
            Assert.That(_menus.GetValue("settings/look.sensitivity"), Is.EqualTo("3"));
        }

        [Test]
        public void AToggle_Flips_AndAChoice_MovesToTheNextOption()
        {
            _menus.Open(_controls);
            _menus.Invoke("settings/look.invert_y");
            Assert.That(_settings.GetBool(LookSettings.InvertYKey), Is.True);

            _menus.Open(_graphics);
            var row = _menus.Current!.Items[0];
            Assert.That(row.Kind, Is.EqualTo(MenuItemKind.Choice));
            Assert.That(row.Options, Is.EqualTo(new[] { "Low", "Medium", "High" }));
            _menus.Invoke("settings/graphics.quality");
            _menus.Invoke("settings/graphics.quality");
            Assert.That(_settings.GetString("graphics.quality"), Is.EqualTo("Low"), "wraps around");
        }

        [Test]
        public void Reset_ResetsThePageOnly()
        {
            _settings.Set(LookSettings.SensitivityKey, "2");
            _settings.Set("graphics.quality", "High");
            _menus.Open(_controls);

            _menus.Invoke("settings/reset");

            Assert.That((_settings.GetFloat(LookSettings.SensitivityKey), _settings.GetString("graphics.quality")), Is.EqualTo((1f, "High")));
        }

        [Test]
        public void AGroupWithNothingInThisScene_SaysSo()
        {
            _menus.Open(_audio);

            Assert.That(_menus.Current!.Items[0].Label, Is.EqualTo(MenuSettingsRows.EmptyLabel));
        }
    }
}
