#nullable enable
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>Where the shipyard camera is, in the preview's space, and where it looks (yaw about up, then pitch, in degrees).</summary>
    public readonly struct ShipyardCameraPose
    {
        public ShipyardCameraPose(Vector3 position, float yaw, float pitch)
        {
            Position = position;
            Yaw = yaw;
            Pitch = pitch;
        }

        public Vector3 Position { get; }
        public float Yaw { get; }

        /// <summary>Positive looks down.</summary>
        public float Pitch { get; }

        public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);
        public Vector3 Forward => Rotation * Vector3.forward;

        public override string ToString() => $"at {Position}, yaw {Yaw:0}, pitch {Pitch:0}";
    }
}
