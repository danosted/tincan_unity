#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Look;
using UnityEngine;

namespace TinCan.Features.ThirdPersonCamera
{
    /// <summary>
    /// The third-person rig: the camera orbits a centre <see cref="ThirdPersonCameraConfig.Height"/> above where the body
    /// is drawn, <see cref="ThirdPersonCameraConfig.Distance"/> behind it along the look direction.
    /// </summary>
    public sealed class ThirdPersonViewRig : IViewRig
    {
        private readonly ThirdPersonCameraConfig _config;

        public ThirdPersonViewRig(ThirdPersonCameraConfig config) => _config = config;

        public void Take(Camera camera, IReadOnlyList<Renderer> body) { }

        public void Release(Camera camera, IReadOnlyList<Renderer> body) { }

        public (Vector3 Position, Quaternion Rotation) Place(in ViewPose pose) => Orbit(pose, _config.Distance, _config.Height);

        public float AimHeight(float eyeHeight) => _config.Height;

        public static (Vector3 Position, Quaternion Rotation) Orbit(in ViewPose pose, float distance, float height)
        {
            Quaternion rotation = Quaternion.Euler(pose.Pitch, pose.Yaw, 0f);
            Vector3 centre = pose.VisualPosition + Vector3.up * height;
            return (centre - rotation * Vector3.forward * distance, rotation);
        }
    }
}
