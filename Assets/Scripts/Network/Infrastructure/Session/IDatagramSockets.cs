#nullable enable
namespace TinCan.Network.Infrastructure.Session
{
    /// <summary>Opens the sockets discovery uses. Null when the port cannot be opened (another program holds it).</summary>
    public interface IDatagramSockets
    {
        /// <summary>A socket bound to <paramref name="port"/> on every interface, shared with other programs that ask the same.</summary>
        IDatagramSocket? Listen(int port);

        /// <summary>A socket on any free port that may broadcast.</summary>
        IDatagramSocket? Broadcaster();
    }
}
