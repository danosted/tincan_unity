#nullable enable
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Gas;
using TinCan.Core.Targeting;
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Tunables for ship cannons (Assets/Settings/Cannon/CannonConfig). A cannon fires a pulsing beam of spirit energy
    /// while the trigger is held, and overheats. Plans: .docs/plans/ray-cannons.md, .docs/plans/cannon-and-hazards.md.
    /// </summary>
    [CreateAssetMenu(fileName = "CannonConfig", menuName = "TinCan/Weapons/Cannon Config")]
    public class CannonConfig : ScriptableObject
    {
        [Header("Firing")]
        [Tooltip("The occupant's fire input bit (Input_Primary).")]
        public GameplayInput? FireInput;
        [Tooltip("Granted to the occupant by the station and activated for each pulse: who may fire (GA_FireCannon, " +
                 "EndsImmediately, no cooldown; the heat limits the beam).")]
        public AbilityDefinition? FireAbility;
        [Tooltip("Applied by each pulse to the collider target it hits (a hazard breaks), and to the cannon's own ship " +
                 "on a misfire (GE_SpiritBeamHit: Instant, Health -damage). Targets that are no colliders answer " +
                 "through ISpiritBeamTargets instead.")]
        public GameplayEffectDefinition? HitEffect;
        [Tooltip("The beam's sweep: radius and tag filters (TD_SpiritBeamSweep).")]
        public TargetingDefinition? Sweep;

        [Header("Beam")]
        [Tooltip("Seconds between pulses while the trigger is held. The first fires on the press.")]
        [Min(0.02f)] public float PulseSeconds = 0.125f;
        [Tooltip("How far the beam reaches, m.")]
        [Min(1f)] public float BeamRange = 400f;

        [Header("Heat")]
        [Tooltip("Seconds of held fire from cold until it overheats.")]
        [Min(0.1f)] public float SecondsToOverheat = 3f;
        [Tooltip("Seconds from overheated to cold while the trigger is up. An overheated cannon fires again only when cold.")]
        [Min(0.1f)] public float SecondsToCool = 2f;

        [Header("Aim (degrees, from the cannon's rest direction)")]
        [Tooltip("Either way from the rest direction. 180: the barrel turns all the way round (a turret), and its yaw wraps.")]
        [Range(0f, 180f)] public float YawLimit = CannonAimLimits.FullTurn;
        [Range(-90f, 0f)] public float MinElevation = -15f;
        [Range(0f, 90f)] public float MaxElevation = 40f;
        [Tooltip("Degrees the barrel turns per mouse count while manning it (the Gunner context's Aim).")]
        [Min(0.01f)] public float AimSensitivity = 0.3f;
        [Tooltip("How fast other peers' barrels follow the replicated aim (1/s).")]
        [Min(0f)] public float ProxyAimSharpness = 15f;

        [Header("Presentation")]
        [Tooltip("The beam's and the glows' material: soft, additive, its colour from the line (URP Particles/Unlit). " +
                 "Required in player builds: a runtime object otherwise keeps the built-in default, which URP draws pink.")]
        public Material? BeamMaterial;
        [Tooltip("The beam's colour at the muzzle and at its end.")]
        public Color MuzzleColor = new(0.55f, 0.95f, 1f);
        public Color EndColor = new(1f, 0.8f, 0.45f);
        [Tooltip("The muzzle glow's colour as the barrel nears overheating.")]
        public Color HotColor = new(1f, 0.42f, 0.2f);
        [Min(0.01f)] public float BeamWidth = 0.35f;
        [Tooltip("How much of the width each pulse throbs: 0 a steady beam, 1 it pinches to nothing between pulses.")]
        [Range(0f, 1f)] public float PulseThrob = 0.55f;
        [Tooltip("Seconds the beam takes to fade once the trigger is let go.")]
        [Min(0.01f)] public float FadeSeconds = 0.12f;
        [Tooltip("The glow at the muzzle when hot, and the bloom where the beam ends, m across.")]
        [Min(0.01f)] public float GlowDiameter = 0.8f;
        [Tooltip("Played at the muzzle on each pulse.")]
        public AudioClip? PulseSound;
        [Range(0f, 1f)] public float PulseVolume = 0.6f;
        [Tooltip("Played at the muzzle when it overheats and starts to vent.")]
        public AudioClip? VentSound;
        [Range(0f, 1f)] public float VentVolume = 0.8f;

        public CannonAimLimits AimLimits => new(YawLimit, MinElevation, MaxElevation);

        /// <summary>Heat one pulse adds (0..1).</summary>
        public float HeatPerPulse => PulseSeconds / SecondsToOverheat;
    }
}
