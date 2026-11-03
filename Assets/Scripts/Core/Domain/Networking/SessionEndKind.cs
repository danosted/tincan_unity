#nullable enable
namespace TinCan.Core.Domain.Networking
{
    public enum SessionEndKind
    {
        /// <summary>This player left (Leave, or quitting the shipyard).</summary>
        Left,
        /// <summary>The server refused or removed this client, with a reason.</summary>
        Refused,
        /// <summary>The connection dropped, or the host stopped, without a reason.</summary>
        ConnectionLost
    }
}
