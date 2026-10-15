#nullable enable
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// Tunables for the grappling hook: what marks a holder, which input fires and reels, how far it reaches and how hard it
    /// pulls. The pull is part of the predicted movement simulation, so owner and server read these same values.
    /// </summary>
    [CreateAssetMenu(fileName = "GrappleHookConfig", menuName = "TinCan/Items/Grapple Hook Config")]
    public sealed class GrappleHookConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The tag the hook's equipped effect grants (State.Carrying.GrappleHook): only its holder can fire.")]
        public GameplayTag? CarryingTag;

        [Tooltip("The input that fires on press and reels while held (Input_Primary).")]
        public GameplayInput? FireInput;

        [Header("Reach")]
        [Tooltip("How far the hook flies, metres from the eye.")]
        [Min(1f)] public float MaxRange = 50f;

        [Tooltip("What the hook can catch on. Triggers never catch it.")]
        public LayerMask HookableLayers = ~0;

        [Tooltip("The line snaps when the holder ends up farther than this from the hook point (a teleport, a respawn).")]
        [Min(1f)] public float BreakDistance = 70f;

        [Header("Reel")]
        [Tooltip("Speed the line pulls the holder toward the hook point, m/s.")]
        [Min(0.1f)] public float ReelSpeed = 18f;

        [Tooltip("How quickly the holder's velocity turns toward the reel, m/s². Higher feels stiffer.")]
        [Min(0.1f)] public float ReelAcceleration = 60f;

        [Header("Climb")]
        [Tooltip("When something stops the reel (a rail, the hull below a deck edge), the holder rises at least this fast, m/s, until over it.")]
        [Min(0f)] public float ClimbSpeed = 6f;

        [Tooltip("A step counts as stopped when it closed less than this fraction of the distance its speed should have covered.")]
        [Range(0f, 1f)] public float StallFraction = 0.35f;

        [Header("Arrival")]
        [Tooltip("Within this distance of the hook point (from the body's centre) the holder has arrived and the hook lets go.")]
        [Min(0.1f)] public float ArriveDistance = 1.5f;

        [Tooltip("Speed kept on arrival, m/s, so the holder does not fly past the point.")]
        [Min(0f)] public float ArriveSpeed = 4f;

        [Tooltip("Upward speed given on arrival, m/s: a hop that carries the holder over a rail or a ledge.")]
        [Min(0f)] public float ArriveHopSpeed = 5f;

        [Header("Line")]
        [Tooltip("Material of the drawn line (unlit).")]
        public Material? LineMaterial;

        [Tooltip("Width of the drawn line, metres.")]
        [Min(0.001f)] public float LineWidth = 0.04f;

        [Tooltip("Child of the held visual the line starts from.")]
        public string LineStartName = "HookTip";

        [Header("Aim marker")]
        [Tooltip("The marker looks this many times MaxRange ahead, so a surface just out of reach shows as out of range.")]
        [Min(1f)] public float MarkerSightFactor = 2f;

        [Tooltip("Marker size as a fraction of its distance from the eye, so it looks the same size near and far.")]
        [Min(0.001f)] public float MarkerScreenSize = 0.012f;

        [Tooltip("Marker material where a press would catch (unlit).")]
        public Material? InRangeMaterial;

        [Tooltip("Marker material where the surface is beyond MaxRange (unlit).")]
        public Material? OutOfRangeMaterial;
    }
}
