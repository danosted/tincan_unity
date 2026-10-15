#nullable enable
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>Where a hook fired from <paramref name="from"/> along <paramref name="direction"/> catches, if anywhere.</summary>
    public interface IGrappleHookQuery
    {
        /// <summary>
        /// The first solid surface within <paramref name="range"/>, skipping triggers and everything under
        /// <paramref name="holder"/> (the holder's own body). <paramref name="frame"/> is the transform the point is fixed to.
        /// </summary>
        bool TryCatch(Vector3 from, Vector3 direction, float range, LayerMask layers, Transform holder,
            out Transform frame, out Vector3 point);
    }
}
