#nullable enable
using UnityEngine;

namespace TinCan.Features.FirstPersonCamera
{
    /// <summary>
    /// The math of held items in first person: they hang from the eyes as the body's visual root does at rest, turned
    /// with a look that trails the camera's a little (sway) but never by more than a cap, so they stay in view.
    /// </summary>
    public static class HeldItemSway
    {
        /// <summary>
        /// The trailing look one frame on: eases toward <paramref name="look"/> at <paramref name="sharpness"/> (per
        /// second, frame-rate independent) and is held within <paramref name="maxAngle"/> degrees of it.
        /// </summary>
        public static Quaternion Trail(Quaternion trailing, Quaternion look, float sharpness, float maxAngle, float deltaTime)
        {
            var followed = Quaternion.Slerp(trailing, look, 1f - Mathf.Exp(-sharpness * deltaTime));
            if (Quaternion.Angle(followed, look) <= maxAngle) return followed;
            return Quaternion.RotateTowards(look, followed, maxAngle);
        }

        /// <summary>
        /// Where the held items' socket sits: <paramref name="eyeHeight"/> below the eyes along the look's own down, so at
        /// a level look it is exactly the body's visual root and every item keeps the pose it was tuned with.
        /// </summary>
        public static (Vector3 Position, Quaternion Rotation) Hold(Vector3 eyes, Quaternion look, float eyeHeight) =>
            (eyes + look * (Vector3.down * eyeHeight), look);
    }
}
