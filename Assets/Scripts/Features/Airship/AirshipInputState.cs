using Unity.Netcode;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Domain Layer: Data structure representing the input state for steering an airship.
    /// </summary>
    public struct AirshipInputState : INetworkSerializable
    {
        public float Throttle; // -1 to 1 (Forward/Backward)
        public float Yaw;      // -1 to 1 (Left/Right)
        public float Pitch;    // -1 to 1 (Up/Down)

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Throttle);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Pitch);
        }
    }
}
