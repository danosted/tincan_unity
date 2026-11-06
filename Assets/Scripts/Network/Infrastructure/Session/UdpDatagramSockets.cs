#nullable enable
using System.Net;
using System.Net.Sockets;

namespace TinCan.Network.Infrastructure.Session
{
    /// <summary>Non-blocking IPv4 UDP sockets from <see cref="System.Net.Sockets"/>, polled by discovery each frame.</summary>
    public sealed class UdpDatagramSockets : IDatagramSockets
    {
        public IDatagramSocket? Listen(int port) => Open(port, shared: true);

        public IDatagramSocket? Broadcaster() => Open(0, shared: false);

        private static IDatagramSocket? Open(int port, bool shared)
        {
            Socket? socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                // Shared: a second game on this machine (MPPM, a dedicated server next to a host) can answer too.
                if (shared) socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.EnableBroadcast = true;
                socket.Blocking = false;
                IgnoreConnectionResets(socket);
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                return new UdpDatagramSocket(socket);
            }
            catch (SocketException)
            {
                socket?.Dispose();
                return null;
            }
        }

        // On Windows an ICMP "port unreachable" for an earlier send makes the next receive throw ConnectionReset; a
        // browser that asked a machine with no game on it would then stop hearing everyone. SIO_UDP_CONNRESET off.
        private static void IgnoreConnectionResets(Socket socket)
        {
            const int SioUdpConnReset = -1744830452;
            try
            {
                socket.IOControl(SioUdpConnReset, new byte[] { 0 }, null);
            }
            catch (SocketException)
            {
            }
            catch (System.PlatformNotSupportedException)
            {
            }
        }

        private sealed class UdpDatagramSocket : IDatagramSocket
        {
            private const int MaxDatagram = 1500;

            private readonly Socket _socket;
            private readonly byte[] _buffer = new byte[MaxDatagram];
            private EndPoint _sender = new IPEndPoint(IPAddress.Any, 0);

            public UdpDatagramSocket(Socket socket)
            {
                _socket = socket;
            }

            public void Send(byte[] bytes, string address, int port)
            {
                try
                {
                    _socket.SendTo(bytes, new IPEndPoint(IPAddress.Parse(address), port));
                }
                catch (SocketException)
                {
                    // No network (offline, a cable out): discovery stays quiet until there is one.
                }
            }

            public bool TryReceive(out byte[] bytes, out string address, out int port)
            {
                bytes = System.Array.Empty<byte>();
                address = string.Empty;
                port = 0;
                try
                {
                    if (_socket.Available == 0) return false;
                    int length = _socket.ReceiveFrom(_buffer, ref _sender);
                    bytes = new byte[length];
                    System.Array.Copy(_buffer, bytes, length);
                    var endpoint = (IPEndPoint)_sender;
                    address = endpoint.Address.ToString();
                    port = endpoint.Port;
                    return true;
                }
                catch (SocketException)
                {
                    return false;
                }
            }

            public void Dispose() => _socket.Dispose();
        }
    }
}
