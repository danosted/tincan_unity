namespace TinCan.Core.Domain.Networking
{
    public enum NetworkState
    {
        Offline,
        Connecting,
        Host,
        Server,
        Client
    }

    public interface INetworkService
    {
        NetworkState State { get; }
        bool IsActive { get; }
        bool IsServer { get; }
        bool IsClient { get; }
        bool IsHost { get; }

        void StartHost();
        void StartServer();
        void StartClient();
        void Shutdown();
    }
}
