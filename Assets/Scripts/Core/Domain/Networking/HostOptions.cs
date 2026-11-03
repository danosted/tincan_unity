#nullable enable
namespace TinCan.Core.Domain.Networking
{
    /// <summary>How to host: with a local player (a listen server) or as a dedicated server, and how many players fit.</summary>
    public readonly struct HostOptions
    {
        public const int DefaultMaxPlayers = 8;

        /// <summary>Players the session lets in, the host's own player included.</summary>
        public readonly int MaxPlayers;
        /// <summary>A dedicated server: no local player.</summary>
        public readonly bool Dedicated;
        /// <summary>The local address to listen on (dedicated only; empty keeps the transport's own).</summary>
        public readonly string ListenAddress;
        /// <summary>The port to listen on (dedicated only; 0 keeps the transport's own).</summary>
        public readonly ushort Port;

        public HostOptions(int maxPlayers = DefaultMaxPlayers, bool dedicated = false, string listenAddress = "", ushort port = 0)
        {
            MaxPlayers = maxPlayers > 0 ? maxPlayers : DefaultMaxPlayers;
            Dedicated = dedicated;
            ListenAddress = listenAddress;
            Port = port;
        }

        public static HostOptions Default => new(DefaultMaxPlayers);
    }
}
