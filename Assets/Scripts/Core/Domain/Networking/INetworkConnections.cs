#nullable enable
using System;

namespace TinCan.Core.Domain.Networking
{
    /// <summary>
    /// The connection layer under a session: what a client sends when it connects, how the server decides whether to let
    /// it in, and who connected or left. Implemented by the network service; used by the session service
    /// (.docs/plans/hosting-and-joining.md).
    /// </summary>
    public interface INetworkConnections
    {
        /// <summary>Bytes the next StartClient (or StartHost, for the host's own client) sends with its connection request.</summary>
        void SetConnectionPayload(byte[] payload);

        /// <summary>
        /// Asked on the server about every connecting client, the host's own client included. Null approves everyone.
        /// </summary>
        Func<ConnectionRequest, ConnectionDecision>? Approval { get; set; }

        /// <summary>A client finished connecting (raised on the server for each client, on a client for itself).</summary>
        event Action<ulong>? ClientConnected;

        /// <summary>
        /// A client left or was refused. On the server: that client's id. On a client: its own id and the reason the
        /// server gave (null when there was none, for example a lost connection).
        /// </summary>
        event Action<ulong, string?>? ClientDisconnected;
    }
}
