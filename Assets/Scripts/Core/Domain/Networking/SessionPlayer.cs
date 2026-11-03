#nullable enable
namespace TinCan.Core.Domain.Networking
{
    /// <summary>A player the server let in, with the name it sent.</summary>
    public readonly struct SessionPlayer
    {
        public readonly ulong ClientId;
        public readonly string Name;

        public SessionPlayer(ulong clientId, string name)
        {
            ClientId = clientId;
            Name = name;
        }
    }
}
