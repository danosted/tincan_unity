#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Audio;
using TinCan.Core.RandomStreams;
using TinCan.Tests.Fakes;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace TinCan.Core.Audio.Tests
{
    /// <summary>The pool builds its voices once, reuses them, and routes each sound to its channel's group.</summary>
    public class AudioOneShotPoolTests
    {
        private const string MixerPath = "Assets/Audio/TinCanMixer.mixer";

        private AudioConfig _config = null!;
        private FakeTimeService _time = null!;
        private AudioOneShotPool _pool = null!;
        private AudioClip _clip = null!;

        [SetUp]
        public void SetUp()
        {
            var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            Assert.That(mixer, Is.Not.Null, MixerPath);
            _config = ScriptableObject.CreateInstance<AudioConfig>();
            _config.Mixer = mixer;
            _config.World = Group(mixer, "World");
            _config.Ui = Group(mixer, "Ui");
            _config.PoolSize = 2;
            _time = new FakeTimeService();
            _pool = new AudioOneShotPool(_config, _time, new RandomSource(1));
            _clip = AudioClip.Create("Bang", 44100, 1, 44100, false); // one second
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void Pool_IsBuiltOnTheFirstSound_AndNeverGrows()
        {
            Assert.That(_pool.Sources, Is.Empty);

            for (int i = 0; i < 5; i++)
            {
                _time.Time = i * 0.1f;
                _pool.PlayOneShot(_clip, AudioChannel.World, Vector3.zero);
            }

            Assert.That(_pool.Sources.Length, Is.EqualTo(2));
            Assert.That(_pool.Sources.Select(s => s.transform.parent).Distinct().Count(), Is.EqualTo(1), "one root");
        }

        [Test]
        public void Sound_GoesToItsChannelsGroup_In3DWithAPosition_And2DWithout()
        {
            _pool.PlayOneShot(_clip, AudioChannel.World, new Vector3(1f, 2f, 3f), 0.5f);
            _pool.PlayOneShot(_clip, AudioChannel.Ui, null);

            var world = _pool.Sources[0];
            var ui = _pool.Sources[1];
            Assert.That(world.outputAudioMixerGroup, Is.SameAs(_config.World));
            Assert.That(world.spatialBlend, Is.EqualTo(1f));
            Assert.That(world.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(world.volume, Is.EqualTo(0.5f));
            Assert.That(ui.outputAudioMixerGroup, Is.SameAs(_config.Ui));
            Assert.That(ui.spatialBlend, Is.EqualTo(0f));
        }

        [Test]
        public void FullPool_CutsOffTheOldestVoice()
        {
            var bang = _clip;
            var ding = AudioClip.Create("Ding", 44100, 1, 44100, false);
            _pool.PlayOneShot(bang, AudioChannel.World, Vector3.zero);
            _time.Time = 0.1f;
            _pool.PlayOneShot(bang, AudioChannel.World, Vector3.zero);
            _time.Time = 0.2f;
            _pool.PlayOneShot(ding, AudioChannel.Ui, null);

            Assert.That(_pool.Sources[0].clip, Is.SameAs(ding));
            Assert.That(_pool.Sources[1].clip, Is.SameAs(bang));
            Object.DestroyImmediate(ding);
        }

        [Test]
        public void PitchJitter_StaysInItsRange()
        {
            for (int i = 0; i < 20; i++)
            {
                _time.Time = i * 2f;
                _pool.PlayOneShot(_clip, AudioChannel.World, Vector3.zero, 1f, 0.1f);
                Assert.That(_pool.Sources[0].pitch, Is.InRange(0.9f, 1.1f));
            }
        }

        private static AudioMixerGroup Group(AudioMixer mixer, string name) =>
            mixer.FindMatchingGroups(string.Empty).Single(g => g.name == name);
    }
}
