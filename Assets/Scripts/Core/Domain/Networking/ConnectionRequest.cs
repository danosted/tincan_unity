#nullable enable
namespace TinCan.Core.Domain.Networking
{
    /// <summary>A client asking the server to let it in, with the payload it sent.</summary>
    public readonly struct ConnectionRequest
    {
        public readonly ulong ClientId;
        /// <summary>The host's own client (always let in: the host is the session).</summary>
        public readonly bool IsHostClient;
        public readonly byte[] Payload;

        public ConnectionRequest(ulong clientId, bool isHostClient, byte[]? payload)
        {
            ClientId = clientId;
            IsHostClient = isHostClient;
            Payload = payload ?? System.Array.Empty<byte>();
        }
    }
}
