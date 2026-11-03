#nullable enable
using TinCan.Core.Domain.Networking;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// A network service whose session role (host, server, client, offline) is set by the test. Starting makes it
    /// active and shutting down makes it inactive; the role stays what the test set.
    /// </summary>
    public sealed class FakeSessionNetworkService : INetworkService
    {
        public NetworkState State => !IsActive ? NetworkState.Offline : IsServer ? (IsClient ? NetworkState.Host : NetworkState.Server) : NetworkState.Client;
        public bool IsActive { get; set; } = true;
        public bool IsServer { get; set; }
        public bool IsClient { get; set; }
        public bool IsHost => IsServer && IsClient;
        public ulong LocalClientId { get; set; }
        public int ShutdownCalls { get; private set; }

        public void SetPlayerPrefab(GameObject prefab) { }
        public void SetConnection(string address, ushort port) { }
        public void SetListenEndpoint(string listenAddress, ushort port) { }
        public void StartHost() => IsActive = true;
        public void StartServer() => IsActive = true;
        public void StartClient() => IsActive = true;

        public void Shutdown()
        {
            ShutdownCalls++;
            IsActive = false;
        }
    }
}
