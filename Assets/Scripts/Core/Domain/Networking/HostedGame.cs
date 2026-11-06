#nullable enable
namespace TinCan.Core.Domain.Networking
{
    /// <summary>The game this peer serves, as discovery shows it to others.</summary>
    public readonly struct HostedGame
    {
        public readonly string Name;
        public readonly int MaxPlayers;

        public HostedGame(string name, int maxPlayers)
        {
            Name = name;
            MaxPlayers = maxPlayers;
        }
    }
}
