#nullable enable
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Pure: the shipyard's free-flying camera, like the Unity Scene view. The mouse turns it (while the look button is
    /// held), WASD flies along the view (forward follows the pitch), Q/E fly straight down and up, the wheel pushes it
    /// straight along the view. A fresh view frames the whole ship from above and to the side. Plan:
    /// .docs/plans/ship-frames.md (S2).
    /// </summary>
    public sealed class ShipyardFlyProcessor
    {
        public ShipyardCameraPose Look(ShipyardCameraPose pose, Vector2 degrees, float maxPitch) =>
            new(pose.Position, pose.Yaw + degrees.x, Mathf.Clamp(pose.Pitch - degrees.y, -maxPitch, maxPitch));

        /// <summary>
        /// Moves the camera: <paramref name="fly"/> (x right, y forward) and <paramref name="rise"/> at <paramref name="speed"/>
        /// m/s for <paramref name="deltaTime"/>, plus <paramref name="push"/> metres straight along the view.
        /// </summary>
        public ShipyardCameraPose Fly(ShipyardCameraPose pose, Vector2 fly, float rise, float push, float speed, float deltaTime)
        {
            var rotation = pose.Rotation;
            var along = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
            var move = (right * fly.x + along * fly.y + Vector3.up * rise) * (speed * deltaTime) + along * push;
            return new ShipyardCameraPose(pose.Position + move, pose.Yaw, pose.Pitch);
        }

        /// <summary>
        /// A view of the whole of <paramref name="bounds"/>: looking at its centre from <paramref name="yaw"/> and
        /// <paramref name="pitch"/>, far enough back that a sphere around it fits the field of view, and never closer than
        /// <paramref name="minDistance"/>.
        /// </summary>
        public ShipyardCameraPose Frame(Bounds bounds, float fieldOfView, float yaw, float pitch, float minDistance)
        {
            float radius = bounds.extents.magnitude;
            float halfAngle = Mathf.Max(1f, fieldOfView * 0.5f) * Mathf.Deg2Rad;
            float distance = Mathf.Max(minDistance, radius / Mathf.Sin(halfAngle));
            var forward = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            return new ShipyardCameraPose(bounds.center - forward * distance, yaw, pitch);
        }
    }
}
