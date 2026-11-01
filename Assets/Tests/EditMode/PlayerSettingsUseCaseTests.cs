#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Settings;
using TinCan.Core.Settings;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Player settings: <see cref="SettingValueProcessor"/> keeps values inside their definition,
    /// <see cref="PlayerSettingsUseCase"/> loads, saves (after a pause, only what differs) and resets, a feature's
    /// contribution joins the core list (<see cref="SettingsCatalog"/>), and <see cref="LookSettings"/> applies them.
    /// </summary>
    public class PlayerSettingsUseCaseTests
    {
        private SettingDefinition _sensitivity = null!;
        private SettingDefinition _invert = null!;
        private SettingDefinition _quality = null!;
        private SettingDefinition _name = null!;
        private FakeSettingsStore _store = null!;
        private FakeTimeService _time = null!;

        [SetUp]
        public void SetUp()
        {
            _sensitivity = SettingDefinition.Slider(LookSettings.SensitivityKey, "Look sensitivity", SettingGroup.Controls, 0.1f, 3f, 0.05f, 1f);
            _invert = SettingDefinition.Toggle(LookSettings.InvertYKey, "Invert Y", SettingGroup.Controls, false);
            _quality = SettingDefinition.Choice("graphics.quality", "Quality", SettingGroup.Graphics, "Medium", "Low", "Medium", "High");
            _name = SettingDefinition.Text("player.name", "Name", SettingGroup.Player, "Deckhand", 8);
            _store = new FakeSettingsStore();
            _time = new FakeTimeService { Time = 10f };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in new Object[] { _sensitivity, _invert, _quality, _name }) Object.DestroyImmediate(asset);
        }

        private PlayerSettingsUseCase Settings() => new(new SettingsCatalog(new[] { _sensitivity, _invert, _quality, _name }), _store, _time);

        [Test]
        public void Normalize_KeepsEachKindInsideItsDefinition()
        {
            Assert.That(SettingValueProcessor.Normalize(_sensitivity, "9"), Is.EqualTo("3"), "clamped");
            Assert.That(SettingValueProcessor.Normalize(_sensitivity, "1.27"), Is.EqualTo("1.25"), "snapped to the step");
            Assert.That(SettingValueProcessor.Normalize(_sensitivity, "fast"), Is.EqualTo("1"), "unreadable: the default");
            Assert.That(SettingValueProcessor.Normalize(_invert, "yes"), Is.EqualTo(bool.FalseString));
            Assert.That(SettingValueProcessor.Normalize(_quality, "Ultra"), Is.EqualTo("Medium"), "not an option: the default");
            Assert.That(SettingValueProcessor.Normalize(_name, "Captain Haddock"), Is.EqualTo("Captain "), "cut, not trimmed");
        }

        [Test]
        public void Values_StartAtTheirDefaults_AndSetIsChecked()
        {
            var settings = Settings();
            var changed = new List<string>();
            settings.Changed += changed.Add;

            Assert.That(settings.GetFloat(LookSettings.SensitivityKey), Is.EqualTo(1f));
            settings.Set(LookSettings.SensitivityKey, "0.01");
            settings.Set(LookSettings.SensitivityKey, "0.1");

            Assert.That(settings.GetFloat(LookSettings.SensitivityKey), Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(changed, Is.EqualTo(new[] { LookSettings.SensitivityKey }), "the same value twice is one change");
        }

        [Test]
        public void Saving_WaitsForAPause_KeepsOnlyChangedValues_AndLoadsBack()
        {
            var settings = Settings();
            settings.Set(LookSettings.SensitivityKey, "1.5");
            settings.Set(LookSettings.InvertYKey, bool.TrueString);
            settings.Tick();
            Assert.That(_store.Saves, Is.Zero, "a dragged slider does not save every frame");

            _time.Time += PlayerSettingsUseCase.SaveDelaySeconds;
            settings.Tick();
            settings.Tick();
            Assert.That(_store.Saves, Is.EqualTo(1));
            Assert.That(_store.Saved, Does.Contain("look.sensitivity").And.Not.Contain("graphics.quality"));

            var again = Settings();
            Assert.That((again.GetFloat(LookSettings.SensitivityKey), again.GetBool(LookSettings.InvertYKey)), Is.EqualTo((1.5f, true)));
        }

        [Test]
        public void ASavedValueWhoseFeatureIsNotLoaded_IsKept()
        {
            _store.Saved = "{\"Values\":[{\"Key\":\"cannon.aim\",\"Value\":\"2\"}]}";
            var settings = Settings();

            settings.Set(LookSettings.SensitivityKey, "2");
            settings.Dispose();

            Assert.That(_store.Saved, Does.Contain("cannon.aim").And.Contain("look.sensitivity"));
        }

        [Test]
        public void ResetGroup_ResetsThatPageOnly()
        {
            var settings = Settings();
            settings.Set(LookSettings.SensitivityKey, "2");
            settings.Set("graphics.quality", "High");

            settings.ResetGroup(SettingGroup.Controls);

            Assert.That((settings.GetFloat(LookSettings.SensitivityKey), settings.GetString("graphics.quality")), Is.EqualTo((1f, "High")));
        }

        [Test]
        public void AnUnknownKey_WarnsOnce_AndReadsAsTheTypesDefault()
        {
            var settings = Settings();
            LogAssert.Expect(LogType.Warning, "[PlayerSettings] No setting 'view.fov' is loaded in this scene; using the default.");

            Assert.That(settings.GetFloat("view.fov"), Is.Zero);
            Assert.That(settings.GetFloat("view.fov"), Is.Zero);
            Assert.That(settings.Has("view.fov"), Is.False);
        }

        [Test]
        public void DefaultsOnly_ReadsAndSavesNothing()
        {
            var store = new DefaultsOnlySettingsStore();
            var settings = new PlayerSettingsUseCase(new SettingsCatalog(new[] { _sensitivity }), store, _time);
            settings.Set(LookSettings.SensitivityKey, "2");
            settings.Dispose();

            Assert.That(store.Load(), Is.Null);
            Assert.That(new PlayerSettingsUseCase(new SettingsCatalog(new[] { _sensitivity }), store, _time).GetFloat(LookSettings.SensitivityKey), Is.EqualTo(1f));
        }

        [Test]
        public void AFeaturesContribution_JoinsTheCoreList()
        {
            var fov = SettingDefinition.Slider("view.fov", "Field of view", SettingGroup.View, 60f, 110f, 1f, 75f);
            var feature = ScriptableObject.CreateInstance<ContributingInstaller>();
            feature.Setting = fov;

            var catalog = SettingsCatalog.From(new[] { _sensitivity }, new FeatureInstallerCatalog(new FeatureInstaller[] { feature }));

            Assert.That(catalog.All.Select(d => d.Key), Is.EqualTo(new[] { LookSettings.SensitivityKey, "view.fov" }));
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(fov);
        }

        [Test]
        public void LookSettings_ScaleTheDelta_AndInvertY()
        {
            var settings = Settings();
            Assert.That(LookSettings.Apply(null, new Vector2(1f, 2f)), Is.EqualTo(new Vector2(1f, 2f)), "no settings: the view's own feel");

            settings.Set(LookSettings.SensitivityKey, "2");
            settings.Set(LookSettings.InvertYKey, bool.TrueString);

            Assert.That(LookSettings.Apply(settings, new Vector2(1f, 2f)), Is.EqualTo(new Vector2(2f, -4f)));
        }

        private sealed class ContributingInstaller : FeatureInstaller, FeatureInstaller.IExtension<SettingDefinition>
        {
            public SettingDefinition? Setting;
            public IEnumerable<SettingDefinition> Contributions { get { if (Setting != null) yield return Setting; } }
            public override void Install(VContainer.IContainerBuilder builder) { }
        }
    }
}
