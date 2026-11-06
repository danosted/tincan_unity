#nullable enable
namespace TinCan.Core.Domain.Networking
{
    /// <summary>A game on this network that answered discovery: where it is, who is in it, and whether this build can join it.</summary>
    public readonly struct DiscoveredGame
    {
        public readonly string Name;
        public readonly string Address;
        public readonly ushort Port;
        public readonly int Players;
        public readonly int MaxPlayers;
        public readonly string Version;
        /// <summary>The host runs this build's version; another version would be refused.</summary>
        public readonly bool SameVersion;
        public readonly bool NeedsPassword;

        public DiscoveredGame(string name, string address, ushort port, int players, int maxPlayers, string version, bool sameVersion, bool needsPassword)
        {
            Name = name;
            Address = address;
            Port = port;
            Players = players;
            MaxPlayers = maxPlayers;
            Version = version;
            SameVersion = sameVersion;
            NeedsPassword = needsPassword;
        }

        public JoinTarget Target => new(Address, Port);

        /// <summary>One game per address and port.</summary>
        public string Key => Target.ToString();

        public bool IsFull => Players >= MaxPlayers;
    }
}
