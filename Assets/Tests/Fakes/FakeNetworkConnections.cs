#nullable enable
using System;
using TinCan.Core.Domain.Networking;

namespace TinCan.Tests.Fakes
{
    /// <summary>The connection side of a network service: the test plays the connecting clients and the disconnects.</summary>
    public sealed class FakeNetworkConnections : INetworkConnections
    {
        public byte[] Payload { get; private set; } = Array.Empty<byte>();
        public Func<ConnectionRequest, ConnectionDecision>? Approval { get; set; }

        public event Action<ulong>? ClientConnected;
        public event Action<ulong, string?>? ClientDisconnected;

        public void SetConnectionPayload(byte[] payload) => Payload = payload;

        /// <summary>A client asks to join with <paramref name="payload"/> (this service's own payload when null).</summary>
        public ConnectionDecision Request(ulong clientId, byte[]? payload = null, bool isHostClient = false) =>
            Approval?.Invoke(new ConnectionRequest(clientId, isHostClient, payload ?? Payload)) ?? ConnectionDecision.Approve();

        /// <summary>Asks to join and, when let in, finishes connecting.</summary>
        public ConnectionDecision Connect(ulong clientId, byte[]? payload = null, bool isHostClient = false)
        {
            var decision = Request(clientId, payload, isHostClient);
            if (decision.Approved) ClientConnected?.Invoke(clientId);
            return decision;
        }

        /// <summary>On a client: the host let this client in (no approval runs on a client).</summary>
        public void ConnectedAsClient(ulong localClientId) => ClientConnected?.Invoke(localClientId);

        public void Disconnect(ulong clientId, string? reason = null) => ClientDisconnected?.Invoke(clientId, reason);
    }
}
