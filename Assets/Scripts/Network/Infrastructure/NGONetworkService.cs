using Unity.Netcode;
using TinCan.Core.Domain.Networking;

namespace TinCan.Network.Infrastructure
{
    public class NGONetworkService : INetworkService
    {
        private NetworkManager Manager => NetworkManager.Singleton;

        public NetworkState State
        {
            get
            {
                if (Manager == null || !Manager.IsListening) return NetworkState.Offline;
                if (Manager.IsHost) return NetworkState.Host;
                if (Manager.IsServer) return NetworkState.Server;
                if (Manager.IsClient) return NetworkState.Client;
                return NetworkState.Connecting;
            }
        }

        public bool IsActive => Manager != null && Manager.IsListening;
        public bool IsServer => Manager != null && Manager.IsServer;
        public bool IsClient => Manager != null && Manager.IsClient;
        public bool IsHost => Manager != null && Manager.IsHost;
        public ulong LocalClientId => Manager != null ? Manager.LocalClientId : 0;

        public void StartHost() => Manager.StartHost();
        public void StartServer() => Manager.StartServer();
        public void StartClient() => Manager.StartClient();
        public void Shutdown() => Manager.Shutdown();
    }
}
