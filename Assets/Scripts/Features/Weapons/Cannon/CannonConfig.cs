#nullable enable
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Gas;
using TinCan.Core.Targeting;
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Tunables for ship cannons (Assets/Settings/Cannon/CannonConfig). A cannon fires a beam of spirit energy, not a
    /// ball. Plans: .docs/plans/ray-cannons.md, .docs/plans/cannon-and-hazards.md.
    /// </summary>
    [CreateAssetMenu(fileName = "CannonConfig", menuName = "TinCan/Weapons/Cannon Config")]
    public class CannonConfig : ScriptableObject
    {
        [Header("Firing")]
        [Tooltip("The occupant's fire input bit (Input_Primary).")]
        public GameplayInput? FireInput;
        [Tooltip("Granted to the occupant by the station; its cooldown is the time between shots, charge included " +
                 "(GA_FireCannon, EndsImmediately).")]
        public AbilityDefinition? FireAbility;
        [Tooltip("Applied to the collider target the beam hits (a hazard breaks), and to the cannon's own ship on a " +
                 "misfire (GE_SpiritBeamHit: Instant, Health -damage). Targets that are no colliders answer through " +
                 "ISpiritBeamTargets instead.")]
        public GameplayEffectDefinition? HitEffect;
        [Tooltip("The beam's sweep: radius and tag filters (TD_SpiritBeamSweep).")]
        public TargetingDefinition? Sweep;

        [Header("Beam")]
        [Tooltip("Seconds from the press to the beam. The barrel still turns while it charges.")]
        [Min(0f)] public float ChargeSeconds = 0.4f;
        [Tooltip("How far the beam reaches, m.")]
        [Min(1f)] public float BeamRange = 400f;

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
        [Min(0.01f)] public float BeamWidth = 0.35f;
        [Tooltip("Seconds the beam stays drawn, thinning out.")]
        [Min(0.01f)] public float BeamSeconds = 0.35f;
        [Tooltip("The glow at the muzzle at full charge, and the bloom where the beam ends, m across.")]
        [Min(0.01f)] public float GlowDiameter = 0.8f;
        [Tooltip("Played at the muzzle when the charge starts: the charge's hum and the beam's release, in one clip.")]
        public AudioClip? FireSound;
        [Range(0f, 1f)] public float FireVolume = 0.8f;

        public CannonAimLimits AimLimits => new(YawLimit, MinElevation, MaxElevation);
    }
}
