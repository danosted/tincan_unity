#nullable enable
namespace TinCan.Network.Infrastructure.Session
{
    /// <summary>
    /// What a host says about its game in a discovery reply; the address is where the reply came from. <see cref="HostId"/>
    /// is random per session, so a host heard twice (by broadcast and on loopback) is one game.
    /// </summary>
    public readonly struct LanGameInfo
    {
        public readonly uint HostId;
        public readonly string Name;
        public readonly string Version;
        public readonly ushort GamePort;
        public readonly int Players;
        public readonly int MaxPlayers;
        public readonly bool NeedsPassword;

        public LanGameInfo(uint hostId, string name, string version, ushort gamePort, int players, int maxPlayers, bool needsPassword)
        {
            HostId = hostId;
            Name = name;
            Version = version;
            GamePort = gamePort;
            Players = players;
            MaxPlayers = maxPlayers;
            NeedsPassword = needsPassword;
        }
    }
}
