using Unity.Netcode;
using UnityEngine;

namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// Domain Layer: Data structure representing the input state of a humanoid character.
    /// Synchronized over the network to allow remote clients to simulate movement.
    /// </summary>
    public struct HumanoidInputState : INetworkSerializable
    {
        public uint Sequence;
        public Vector3 MovementDirection;
        public bool IsJumping;
        public bool IsSprinting;
        /// <summary>Look yaw relative to the yaw of the platform underfoot (world yaw when there is none).</summary>
        public Quaternion LookRotation;

        /// <summary>
        /// Aim pitch in degrees from the look; positive looks down (Unity's convention). Travels with the rest
        /// of the input so the server can rebuild where the player aims (targeting) instead of trusting a client target.
        /// </summary>
        public float LookPitch;

        /// <summary>
        /// A 64-bit mask where each bit represents a specific GameplayInput that is currently active.
        /// </summary>
        public ulong ActiveInputMask;

        /// <summary>
        /// While occupying a station that aims (a cannon): its aim in degrees in the station's own frame, x yaw and y
        /// elevation (positive up), already within the station's limits. Written by the station's
        /// <see cref="IHumanoidInputContributor"/>; zero otherwise. The body's look stays where it was.
        /// </summary>
        public Vector2 StationAim;

        /// <summary>
        /// While occupying a station that steers (the helm): its axes, each -1..1. For the helm x is throttle, y yaw and
        /// z pitch. Written by the station's <see cref="IHumanoidInputContributor"/>; zero otherwise.
        /// </summary>
        public Vector3 StationAxes;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref MovementDirection);
            serializer.SerializeValue(ref IsJumping);
            serializer.SerializeValue(ref IsSprinting);
            serializer.SerializeValue(ref LookRotation);
            serializer.SerializeValue(ref LookPitch);
            serializer.SerializeValue(ref ActiveInputMask);
            serializer.SerializeValue(ref StationAim);
            serializer.SerializeValue(ref StationAxes);
        }
    }
}
