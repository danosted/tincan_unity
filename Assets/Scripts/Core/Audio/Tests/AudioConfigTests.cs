#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Audio;
using UnityEngine.Audio;

namespace TinCan.Core.Audio.Tests
{
    /// <summary>The shipped config routes every channel to the mixer group of the same name, with its volume exposed.</summary>
    public class AudioConfigTests
    {
        private const string ConfigPath = "Assets/Audio/AudioConfig.asset";

        [Test]
        public void ShippedConfig_MapsEveryChannelToItsGroup()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, ConfigPath);
            Assert.That(config.Mixer, Is.Not.Null, "AudioConfig.Mixer");

            foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
            {
                var group = config.GroupFor(channel);
                Assert.That(group, Is.Not.Null, $"{channel} has no group");
                Assert.That(group!.name, Is.EqualTo(channel.ToString()));
                Assert.That(group.audioMixer, Is.SameAs(config.Mixer), $"{channel} is on another mixer");
            }
        }

        [Test]
        public void ShippedMixer_ExposesEveryGroupsVolume()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigPath);
            AudioMixer mixer = config.Mixer!;

            foreach (var name in Enum.GetNames(typeof(AudioChannel)).Prepend("Master"))
                Assert.That(mixer.GetFloat($"Vol_{name}", out _), Is.True, $"Vol_{name} is not exposed");
        }

        [Test]
        public void ShippedInstaller_UsesTheShippedConfig()
        {
            var installer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioFeatureInstaller>(
                "Assets/Resources/Installers/AudioFeatureInstaller.asset");
            Assert.That(installer, Is.Not.Null);

            var config = new UnityEditor.SerializedObject(installer).FindProperty("_config").objectReferenceValue;
            Assert.That(config, Is.SameAs(UnityEditor.AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigPath)));
        }
    }
}
