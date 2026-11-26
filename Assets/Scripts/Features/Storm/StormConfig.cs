#nullable enable
using TinCan.Features.Atmosphere;
using TinCan.Features.CloudBoundary.Atmosphere;
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

        [Tooltip("The volumetric clouds at full weight: the overcast closing in (denser, more of the sky covered, even, dark cores).")]
        public CloudWeatherTarget Clouds;

        [Header("Lightning (mood only)")]
        [Tooltip("Strikes per minute in a raging storm, seen from anywhere; fewer as it is cured, none below MinWeight. (In the lab's slider mode, by the weight where you are.)")]
        [Min(0f)] public float StrikesPerMinute = 12f;
        [Tooltip("Below this weight there is no lightning.")]
        [Range(0f, 1f)] public float LightningMinWeight = 0.25f;
        [Tooltip("How long a flash lasts, seconds (it flickers twice).")]
        [Min(0.02f)] public float FlashSeconds = 0.35f;
        [Tooltip("The lab's slider mode only: seconds from a flash to its thunder, near strikes (a heavy storm) sooner. In the world the thunder comes at the speed of sound from where the strike is.")]
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

        [Header("The heart and the cure (ST5); scaled by the zone's severity, 1 to 3")]
        [Tooltip("The heart floats this far above the ship that sails in, m, so the guns fire up at it, clear of the rails.")]
        public float HeartAboveShip = 10f;
        [Tooltip("At severity 2 the heart wanders this far from the zone's centre, at 3 twice as far, m (still at 1).")]
        [Min(0f)] public float HeartWander = 25f;
        [Tooltip("Seconds for one round of the heart's wander at severity 2; faster at 3.")]
        [Min(1f)] public float HeartWanderSeconds = 60f;
        [Tooltip("Targets appear this near the heart (min, max), m, around it.")]
        public Vector2 TargetRing = new(10f, 20f);
        [Tooltip("And this far above or below it, m.")]
        [Min(0f)] public float TargetHeight = 5f;
        [Tooltip("A beam passing this near a target's centre hits it, m.")]
        [Min(0.1f)] public float TargetRadius = 2.5f;
        [Tooltip("How long a target stays up at severity 1, s (less in harder storms). One target at a time.")]
        [Min(0.5f)] public float TargetSeconds = 4f;
        [Tooltip("The pause before the next target, s.")]
        [Min(0f)] public float TargetGapSeconds = 0.5f;
        [Tooltip("Hits that cure the storm at severity 1 (two more for each step up).")]
        [Min(1)] public int HitsToCure = 3;

        [Header("Gusts (ST3): waves of sideways push on the ship, by the storm's fury (deeper, and raging) and severity")]
        [Tooltip("A full gust's push at the eye's edge at severity 1, m/s of sideways drift (half again for each step up).")]
        [Min(0f)] public float GustSpeed = 6f;
        [Tooltip("A gust rises and falls over this many seconds.")]
        [Min(0f)] public float GustSeconds = 4f;
        [Tooltip("Gusts come in slots this long, s: at most one a slot (the rest of it is calm).")]
        [Min(0.5f)] public float GustSlotSeconds = 8f;
        [Tooltip("The chance a slot holds a gust at full fury (fewer as the fury falls).")]
        [Range(0f, 1f)] public float GustChance = 0.75f;
        [Tooltip("A gust blows along the storm's swirling wind, swung up to this many degrees off it.")]
        [Range(0f, 90f)] public float GustSwing = 35f;
        [Tooltip("How much a full gust swells the wind's sound (1: twice as loud).")]
        [Min(0f)] public float GustWindSwell = 0.8f;

        [Header("Debris (ST4): sky hazards on the wind, thrown at the ship (SkyHazards' prefab and impact)")]
        [Tooltip("Pieces a minute thrown at a ship at full fury at severity 1 (half again for each step up).")]
        [Min(0f)] public float DebrisPerMinute = 10f;
        [Tooltip("They come from this far upwind of the ship, m.")]
        [Min(10f)] public float DebrisDistance = 140f;
        [Tooltip("And fly at this speed on the wind, m/s.")]
        [Min(1f)] public float DebrisSpeed = 20f;
        [Tooltip("Each is aimed this far off the ship at most, m (across the wind, up or down): some hit, some miss.")]
        public Vector2 DebrisMiss = new(14f, 6f);

        [Header("The heart's look")]
        [Tooltip("The heart and its targets: soft, additive, coloured per object (URP Unlit, transparent, additive).")]
        public Material? GlowMaterial;
        public Color HeartColor = new(0.55f, 0.95f, 1f);
        public Color TargetColor = new(1f, 0.8f, 0.45f);
        [Min(0.1f)] public float HeartDiameter = 8f;
        [Min(0.1f)] public float TargetDiameter = 3f;
        [Tooltip("The heart's pulse, beats per second.")]
        [Min(0f)] public float HeartPulseRate = 0.8f;
    }
}
