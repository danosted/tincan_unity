#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Audio;
using TinCan.Core.Domain.Features;
using TinCan.Tests.Fakes;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace TinCan.Core.Audio.Tests
{
    /// <summary>Batch mode (dedicated server, bots) and a missing config play nothing; otherwise the pool plays.</summary>
    public class AudioFeatureInstallerTests
    {
        [Test]
        public void SilentPlayer_InBatchModeOrWithoutConfig()
        {
            var config = ScriptableObject.CreateInstance<AudioConfig>();

            Assert.That(AudioFeatureInstaller.UsesSilentPlayer(batchMode: true, config), Is.True);
            Assert.That(AudioFeatureInstaller.UsesSilentPlayer(batchMode: false, null), Is.True);
            Assert.That(AudioFeatureInstaller.UsesSilentPlayer(batchMode: false, config), Is.False);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Installer_IsCore()
        {
            var installer = ScriptableObject.CreateInstance<AudioFeatureInstaller>();

            Assert.That(FeatureInstallerCatalog.IsCore(installer), Is.True);
            Object.DestroyImmediate(installer);
        }

        [Test]
        public void Installer_WithoutConfig_RegistersASilentPlayer()
        {
            var installer = ScriptableObject.CreateInstance<AudioFeatureInstaller>();
            var builder = new ContainerBuilder();
            builder.RegisterInstance<TinCan.Core.Domain.ITimeService>(new FakeTimeService());

            installer.Install(builder);
            using var container = builder.Build();

            Assert.That(container.Resolve<IAudioPlayer>(), Is.TypeOf<SilentAudioPlayer>());
            Object.DestroyImmediate(installer);
        }
    }
}
