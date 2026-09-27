#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Features.Targeting
{
    /// <summary>Where a query aims from.</summary>
    public enum AimSource
    {
        /// <summary>From the eye, along the body's facing (yaw only; facing follows the look input).</summary>
        BodyForward,
        /// <summary>From a point in body space (for example a net head in front of the player), along the body's facing.</summary>
        BodyOffset,
        /// <summary>From the eye, along the aim: the body's facing pitched by the replicated look pitch.</summary>
        EyeAim,
        /// <summary>
        /// From the third-person camera's orbit centre, along the aim: what sits under the camera's view centre. Starts
        /// at the orbit centre rather than the camera itself, so rigging behind the player is never picked.
        /// </summary>
        CameraAim
    }

    public enum TargetShape
    {
        /// <summary>
        /// Physics ray (or sphere cast with Radius > 0) along the aim. Finds targetables by their colliders (the registry is
        /// not needed); stops at the first solid collider that is not a target. Range is measured to the hit.
        /// </summary>
        Ray,
        /// <summary>Everything within Range inside the horizontal and vertical angles around the aim. Forgiving; no colliders needed.</summary>
        Cone,
        /// <summary>Everything within Radius of the aim source point.</summary>
        Sphere
    }

    public enum TargetSelection
    {
        Nearest,
        /// <summary>Smallest horizontal angle to the aim, then nearest.</summary>
        BestAligned,
        /// <summary>First along the ray; for other shapes the same as Nearest.</summary>
        FirstHit
    }

    /// <summary>
    /// How one context acquires a target: aim source, shape, gameplay-tag filters on the target's GAS controller,
    /// selection rule and optional line of sight. Referenced by abilities (AbilityDefinition.Targeting) and, later,
    /// interactions. Owner and server run the same definition; the server's answer is authoritative.
    /// </summary>
    [CreateAssetMenu(fileName = "TD_New", menuName = "TinCan/Targeting/Targeting Definition")]
    public class TargetingDefinition : ScriptableObject
    {
        [Header("Aim")]
        public AimSource Source = AimSource.BodyForward;
        [Tooltip("Body-space offset of the aim source point (BodyOffset only).")]
        public Vector3 SourceOffset;

        [Header("Shape")]
        public TargetShape Shape = TargetShape.Cone;
        [Min(0f)] public float Range = 2.5f;
        [Tooltip("Ray: sphere-cast radius (0 = thin ray). Sphere: the sphere's radius.")]
        [Min(0f)] public float Radius;
        [Tooltip("Cone: full horizontal width in degrees.")]
        [Range(1f, 360f)] public float HorizontalAngle = 100f;
        [Tooltip("Cone: full vertical height in degrees, around the horizontal plane.")]
        [Range(1f, 180f)] public float VerticalAngle = 120f;

        [Header("Filter (gameplay tags on the target's ability controller)")]
        public List<GameplayTag> RequiredTags = new();
        public List<GameplayTag> BlockedTags = new();

        [Header("Selection")]
        public TargetSelection Selection = TargetSelection.Nearest;

        [Header("Line of sight")]
        public bool RequireLineOfSight;
        [Tooltip("Colliders that block line of sight from the aim source to a candidate (triggers are ignored).")]
        public LayerMask BlockingMask = ~0;
    }
}
