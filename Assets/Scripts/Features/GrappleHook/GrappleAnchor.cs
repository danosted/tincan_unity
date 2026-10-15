#nullable enable
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// Where a hook caught: a point fixed to the thing it hit (so it moves with a flying ship), and the inputs it held
    /// for. <see cref="AttachedSequence"/> is the input that fired it; <see cref="ReleasedSequence"/> the first input it no
    /// longer pulled on (null while it still holds). A replay pulls only for inputs inside that window.
    /// </summary>
    public readonly struct GrappleAnchor
    {
        public readonly Transform Frame;
        public readonly Vector3 LocalPoint;
        public readonly uint AttachedSequence;
        public readonly uint? ReleasedSequence;

        public GrappleAnchor(Transform frame, Vector3 localPoint, uint attachedSequence, uint? releasedSequence = null)
        {
            Frame = frame;
            LocalPoint = localPoint;
            AttachedSequence = attachedSequence;
            ReleasedSequence = releasedSequence;
        }

        public bool IsHolding => ReleasedSequence == null;

        /// <summary>The thing it caught on still exists here and is in the world (pooled island pieces are switched off).</summary>
        public bool IsValid => Frame != null && Frame.gameObject.activeInHierarchy;

        public Vector3 WorldPoint => Frame.TransformPoint(LocalPoint);

        public GrappleAnchor ReleasedAt(uint sequence) => new(Frame, LocalPoint, AttachedSequence, sequence);

        /// <summary>Whether the hook pulled on input <paramref name="sequence"/> (sequence numbers may wrap).</summary>
        public bool CoversInput(uint sequence) =>
            (int)(sequence - AttachedSequence) >= 0 && (ReleasedSequence is not { } released || (int)(sequence - released) < 0);
    }
}
