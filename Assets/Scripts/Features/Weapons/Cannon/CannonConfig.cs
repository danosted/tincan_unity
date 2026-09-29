#nullable enable
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Gas;
using TinCan.Core.Targeting;
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>Tunables for ship cannons (Assets/Settings/Cannon/CannonConfig). Plan: .docs/plans/cannon-and-hazards.md.</summary>
    [CreateAssetMenu(fileName = "CannonConfig", menuName = "TinCan/Weapons/Cannon Config")]
    public class CannonConfig : ScriptableObject
    {
        [Header("Firing")]
        [Tooltip("The occupant's fire input bit (Input_Primary).")]
        public GameplayInput? FireInput;
        [Tooltip("Granted to the occupant by the station; its cooldown is the reload (GA_FireCannon, EndsImmediately).")]
        public AbilityDefinition? FireAbility;
        [Tooltip("Applied to what a ball hits (GE_CannonballHit: Instant, Health -damage).")]
        public GameplayEffectDefinition? HitEffect;
        [Tooltip("The sweep a ball makes each tick: radius and tag filters (TD_CannonballSweep).")]
        public TargetingDefinition? Sweep;

        [Header("Ballistics")]
        [Min(1f)] public float MuzzleSpeed = 70f;
        [Min(0f)] public float Gravity = 9.81f;
        [Min(0.1f)] public float MaxLifetime = 6f;
        [Min(1f)] public float MaxRange = 400f;

        [Header("Aim (degrees, from the cannon's rest direction)")]
        [Range(0f, 180f)] public float YawLimit = 75f;
        [Range(-90f, 0f)] public float MinElevation = -15f;
        [Range(0f, 90f)] public float MaxElevation = 40f;
        [Tooltip("How fast other peers' barrels follow the replicated aim (1/s).")]
        [Min(0f)] public float ProxyAimSharpness = 15f;

        [Header("Presentation")]
        [Min(0.01f)] public float BallDiameter = 0.5f;
        [Tooltip("Seconds over which a drawn ball moves from this peer's muzzle onto the server's path.")]
        [Min(0f)] public float MuzzleBlendSeconds = 0.2f;
        [Tooltip("Seconds of flight the local occupant's aiming arc shows (0 hides it).")]
        [Min(0f)] public float AimPreviewSeconds = 2.5f;
        [Range(2, 128)] public int AimPreviewPoints = 40;

        public Vector3 GravityVector => Vector3.down * Gravity;
        public CannonAimLimits AimLimits => new(YawLimit, MinElevation, MaxElevation);
        public CannonballLimits BallLimits => new(MaxLifetime, MaxRange);
    }
}
