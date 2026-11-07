#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Core.Audio.Tests
{
    /// <summary>
    /// The volume settings drive the mixer: nothing at construction (Unity ignores SetFloat in Awake), every loaded volume
    /// on the first tick, a change on the next tick, 1 as the authored level, 0 as silent, and the authored levels back
    /// when the scope ends.
    /// </summary>
    public class AudioVolumeBinderTests
    {
        private FakeMixer _mixer = null!;
        private FakeSettings _settings = null!;

        [SetUp]
        public void SetUp()
        {
            _mixer = new FakeMixer();
            _settings = new FakeSettings(AudioVolumeBinder.Volumes.Select(v => v.Key));
        }

        [TearDown]
        public void TearDown() => _settings.Dispose();

        [Test]
        public void NothingIsSet_BeforeTheFirstTick()
        {
            using var binder = new AudioVolumeBinder(_mixer, _settings);

            Assert.That(_mixer.Set, Is.Empty);
        }

        [Test]
        public void FirstTick_SetsEveryVolume()
        {
            _settings.Values[AudioVolumeBinder.WorldKey] = 0.5f;
            using var binder = new AudioVolumeBinder(_mixer, _settings);

            binder.Tick();

            Assert.That(_mixer.Set.Keys, Is.EquivalentTo(AudioVolumeBinder.Volumes.Select(v => v.Parameter)));
            Assert.That(_mixer.Set["Vol_Master"], Is.EqualTo(0f).Within(0.01f));
            Assert.That(_mixer.Set["Vol_World"], Is.EqualTo(-6.02f).Within(0.01f));
        }

        [Test]
        public void AVolume_IsBelowTheAuthoredLevel()
        {
            _mixer.Authored["Vol_Music"] = -10f;
            _settings.Values[AudioVolumeBinder.MusicKey] = 0.1f;
            using var binder = new AudioVolumeBinder(_mixer, _settings);

            binder.Tick();

            Assert.That(_mixer.Set["Vol_Music"], Is.EqualTo(-30f).Within(0.01f));
        }

        [Test]
        public void Zero_IsSilent_WhateverTheAuthoredLevel()
        {
            _mixer.Authored["Vol_Ui"] = 6f;
            _settings.Values[AudioVolumeBinder.UiKey] = 0f;
            using var binder = new AudioVolumeBinder(_mixer, _settings);

            binder.Tick();

            Assert.That(_mixer.Set["Vol_Ui"], Is.EqualTo(VolumeToDecibelsProcessor.Silent));
        }

        [Test]
        public void AChange_AppliesOnTheNextTick_FromTheAuthoredLevel()
        {
            _mixer.Authored["Vol_World"] = -3f;
            using var binder = new AudioVolumeBinder(_mixer, _settings);
            binder.Tick();

            _settings.Change(AudioVolumeBinder.WorldKey, 0.5f);
            Assert.That(_mixer.Set["Vol_World"], Is.EqualTo(-3f).Within(0.01f), "applied before the tick");

            binder.Tick();
            Assert.That(_mixer.Set["Vol_World"], Is.EqualTo(-9.02f).Within(0.01f));
        }

        [Test]
        public void AnotherSettingsChange_SetsNothing()
        {
            using var binder = new AudioVolumeBinder(_mixer, _settings);
            binder.Tick();
            _mixer.Set.Clear();

            _settings.Change("look.sensitivity", 2f);
            binder.Tick();

            Assert.That(_mixer.Set, Is.Empty);
        }

        [Test]
        public void AVolumeWithNoDefinition_KeepsItsAuthoredLevel()
        {
            _settings.Dispose();
            _settings = new FakeSettings(new[] { AudioVolumeBinder.MasterKey });
            using var binder = new AudioVolumeBinder(_mixer, _settings);

            binder.Tick();

            Assert.That(_mixer.Set.Keys, Is.EquivalentTo(new[] { "Vol_Master" }));
        }

        [Test]
        public void WithoutSettings_TheMixerIsLeftAlone()
        {
            using var binder = new AudioVolumeBinder(_mixer, null);

            binder.Tick();

            Assert.That(_mixer.Set, Is.Empty);
        }

        [Test]
        public void Dispose_ClearsTheLevelsAndStopsListening()
        {
            var binder = new AudioVolumeBinder(_mixer, _settings);
            binder.Tick();

            binder.Dispose();
            _settings.Change(AudioVolumeBinder.WorldKey, 0.5f);
            binder.Tick();

            Assert.That(_mixer.Set, Is.Empty);
            Assert.That(_mixer.Cleared, Is.EquivalentTo(AudioVolumeBinder.Volumes.Select(v => v.Parameter)));
        }

        [Test]
        public void Installer_ContributesItsVolumeSettings()
        {
            var installer = ScriptableObject.CreateInstance<AudioFeatureInstaller>();
            var volume = SettingDefinition.Slider(AudioVolumeBinder.MasterKey, "Master volume", SettingGroup.Audio, 0f, 1f, 0.05f, 1f);
            var field = typeof(AudioFeatureInstaller).GetField("_volumes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            field.SetValue(installer, new List<SettingDefinition> { volume, null! });

            Assert.That(installer.Contributions, Is.EqualTo(new[] { volume }));
            Object.DestroyImmediate(volume);
            Object.DestroyImmediate(installer);
        }

        [Test]
        public void ShippedInstaller_ContributesEveryVolume_OnTheAudioPage()
        {
            const string path = "Assets/Resources/Installers/AudioFeatureInstaller.asset";
            var installer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioFeatureInstaller>(path);
            Assert.That(installer, Is.Not.Null, path);

            var contributed = installer.Contributions.ToList();
            Assert.That(contributed.Select(d => d.Key), Is.EquivalentTo(AudioVolumeBinder.Volumes.Select(v => v.Key)));
            foreach (var definition in contributed)
            {
                Assert.That(definition.Group, Is.EqualTo(SettingGroup.Audio), definition.Key);
                Assert.That(definition.Kind, Is.EqualTo(SettingKind.Slider), definition.Key);
                Assert.That(definition.Min, Is.EqualTo(0f), definition.Key);
                Assert.That(definition.Max, Is.EqualTo(1f), definition.Key);
                Assert.That(definition.Default, Is.EqualTo("1"), definition.Key);
            }
        }

        private sealed class FakeMixer : IAudioMixerLevels
        {
            public readonly Dictionary<string, float> Authored = new();
            public readonly Dictionary<string, float> Set = new();
            public readonly List<string> Cleared = new();

            public bool TryGetLevel(string parameter, out float decibels)
            {
                if (Set.TryGetValue(parameter, out decibels)) return true;
                decibels = Authored.TryGetValue(parameter, out float authored) ? authored : 0f;
                return true;
            }

            public void SetLevel(string parameter, float decibels) => Set[parameter] = decibels;

            public void ClearLevel(string parameter)
            {
                Set.Remove(parameter);
                Cleared.Add(parameter);
            }
        }

        private sealed class FakeSettings : IPlayerSettings, IDisposable
        {
            private readonly List<SettingDefinition> _definitions;
            public readonly Dictionary<string, float> Values = new();

            public FakeSettings(IEnumerable<string> keys)
            {
                _definitions = keys.Select(k => SettingDefinition.Slider(k, k, SettingGroup.Audio, 0f, 1f, 0.05f, 1f)).ToList();
                foreach (var definition in _definitions) Values[definition.Key] = 1f;
            }

            public IReadOnlyList<SettingDefinition> Definitions => _definitions;
            public event Action<string>? Changed;

            public bool Has(string key) => _definitions.Any(d => d.Key == key);
            public float GetFloat(string key) => Values.TryGetValue(key, out float value) ? value : 0f;
            public bool GetBool(string key) => false;
            public string GetString(string key) => string.Empty;
            public void Set(string key, string value) => Change(key, float.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
            public void ResetGroup(SettingGroup group) { }

            public void Change(string key, float value)
            {
                Values[key] = value;
                Changed?.Invoke(key);
            }

            public void Dispose()
            {
                foreach (var definition in _definitions) Object.DestroyImmediate(definition);
            }
        }
    }
}
