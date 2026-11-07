#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using TinCan.Core.Domain.Audio;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Settings;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Core.Audio
{
    /// <summary>
    /// Audio (core: loads in every scene). Registers <see cref="IAudioPlayer"/>: the mixer-routed one-shot pool, or a
    /// silent player in batch mode (dedicated server, bots) and when no config is set. With the pool it also registers
    /// <see cref="AudioVolumeBinder"/>, which applies the volume settings this installer contributes to the mixer.
    /// See .docs/plans/audio-mixer.md.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioFeatureInstaller", menuName = "TinCan/Features/Audio Feature Installer")]
    public class AudioFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<SettingDefinition>
    {
        [Tooltip("The mixer and its groups (Assets/Audio/AudioConfig).")]
        [SerializeField] private AudioConfig? _config;
        [Tooltip("The Audio page's volume sliders (Assets/Settings/PlayerSettings/Setting_Volume*), keyed as AudioVolumeBinder.Volumes.")]
        [SerializeField] private List<SettingDefinition> _volumes = new();

        public IEnumerable<SettingDefinition> Contributions
        {
            get
            {
                foreach (var volume in _volumes)
                    if (volume != null) yield return volume;
            }
        }

        public override void Install(IContainerBuilder builder)
        {
            var config = _config;
            if (UsesSilentPlayer(Application.isBatchMode, config))
            {
                builder.Register<SilentAudioPlayer>(Lifetime.Singleton).As<IAudioPlayer>();
                return;
            }

            builder.RegisterInstance(config);
            builder.Register<AudioOneShotPool>(Lifetime.Singleton).As<IAudioPlayer>();

            var mixer = config.Mixer;
            if (mixer != null)
                builder.Register(resolver => new AudioVolumeBinder(new AudioMixerLevels(mixer), resolver.ResolveOrDefault<IPlayerSettings>()), Lifetime.Singleton)
                    .As<ITickable>();
        }

        /// <summary>Batch mode plays nothing (nobody listens and it would only cost CPU); neither does a missing config.</summary>
        public static bool UsesSilentPlayer(bool batchMode, [NotNullWhen(false)] AudioConfig? config) => batchMode || config == null;
    }
}
