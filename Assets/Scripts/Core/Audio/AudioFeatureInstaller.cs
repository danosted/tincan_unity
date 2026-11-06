#nullable enable
using System.Diagnostics.CodeAnalysis;
using TinCan.Core.Domain.Audio;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Core.Audio
{
    /// <summary>
    /// Audio (core: loads in every scene). Registers <see cref="IAudioPlayer"/>: the mixer-routed one-shot pool, or a
    /// silent player in batch mode (dedicated server, bots) and when no config is set. See .docs/plans/audio-mixer.md.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioFeatureInstaller", menuName = "TinCan/Features/Audio Feature Installer")]
    public class AudioFeatureInstaller : FeatureInstaller
    {
        [Tooltip("The mixer and its groups (Assets/Audio/AudioConfig).")]
        [SerializeField] private AudioConfig? _config;

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
        }

        /// <summary>Batch mode plays nothing (nobody listens and it would only cost CPU); neither does a missing config.</summary>
        public static bool UsesSilentPlayer(bool batchMode, [NotNullWhen(false)] AudioConfig? config) => batchMode || config == null;
    }
}
