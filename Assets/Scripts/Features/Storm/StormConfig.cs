#nullable enable
using TinCan.Features.Atmosphere;
using UnityEngine;
using UnityEngine.Audio;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// How a storm looks and sounds (Assets/Settings/Storm/StormConfig): its light and air at full weight, its lightning,
    /// and its sound beds. Weight is depth times strength (<see cref="StormProcessor.Weight"/>). Built by TinCan > Dev >
    /// Storm > Build Lab; tuned by eye in Lab_Storm. Plan: .docs/plans/storm-crossing.md (ST1).
    /// </summary>
    [CreateAssetMenu(fileName = "StormConfig", menuName = "TinCan/Storm/Storm Config")]
    public sealed class StormConfig : ScriptableObject
    {
        [Tooltip("The light and air at full weight (the heart of a raging storm). Slate and plum, never grey (ART_DIRECTION pillar 3).")]
        public WeatherLook Look;

        [Header("Lightning (mood only)")]
        [Tooltip("Strikes per minute at full weight; fewer as the weight falls, none below MinWeight.")]
        [Min(0f)] public float StrikesPerMinute = 12f;
        [Tooltip("Below this weight there is no lightning.")]
        [Range(0f, 1f)] public float LightningMinWeight = 0.25f;
        [Tooltip("How long a flash lasts, seconds (it flickers twice).")]
        [Min(0.02f)] public float FlashSeconds = 0.35f;
        [Tooltip("Seconds from a flash to its thunder: near strikes (a heavy storm) sooner, far ones later.")]
        public Vector2 ThunderDelay = new(0.4f, 4f);
        [Tooltip("Seeds where the strikes fall in time, the same on every peer.")]
        public int Seed = 1;

        [Header("Sound (stand-ins synthesized by the lab builder; swap in real clips)")]
        public AudioMixerGroup? Output;
        public AudioClip? WindLoop;
        [Range(0f, 1f)] public float WindVolume = 0.8f;
        public AudioClip? RainLoop;
        [Range(0f, 1f)] public float RainVolume = 0.6f;
        public AudioClip? HeartLoop;
        [Range(0f, 1f)] public float HeartVolume = 0.7f;
        [Tooltip("How close to the heart the pulse begins: depth above this is heard, louder toward 1.")]
        [Range(0f, 1f)] public float HeartFromDepth = 0.6f;
        public AudioClip[] Thunder = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float ThunderVolume = 0.9f;
    }
}
