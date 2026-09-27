#nullable enable
using TinCan.Core.Domain.Networking;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>A network service whose session role (host, server, client, offline) is set by the test.</summary>
    public sealed class FakeSessionNetworkService : INetworkService
    {
        public NetworkState State => !IsActive ? NetworkState.Offline : IsServer ? (IsClient ? NetworkState.Host : NetworkState.Server) : NetworkState.Client;
        public bool IsActive { get; set; } = true;
        public bool IsServer { get; set; }
        public bool IsClient { get; set; }
        public bool IsHost => IsServer && IsClient;
        public ulong LocalClientId { get; set; }

        public void SetPlayerPrefab(GameObject prefab) { }
        public void SetConnection(string address, ushort port) { }
        public void StartHost() { }
        public void StartServer() { }
        public void StartClient() { }
        public void Shutdown() { }
    }
}
