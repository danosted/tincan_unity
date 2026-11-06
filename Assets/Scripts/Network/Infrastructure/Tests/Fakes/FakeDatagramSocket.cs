#nullable enable
using System.Collections.Generic;
using TinCan.Network.Infrastructure.Session;

namespace TinCan.Network.Infrastructure.Tests
{
    /// <summary>A socket the test delivers datagrams to and reads what was sent from.</summary>
    public sealed class FakeDatagramSocket : IDatagramSocket
    {
        private readonly Queue<(byte[] Bytes, string Address, int Port)> _inbox = new();

        public FakeDatagramSocket(int port)
        {
            Port = port;
        }

        public int Port { get; }
        public List<(byte[] Bytes, string Address, int Port)> Sent { get; } = new();
        public bool Disposed { get; private set; }

        public void Deliver(byte[] bytes, string address, int port) => _inbox.Enqueue((bytes, address, port));

        public void Send(byte[] bytes, string address, int port) => Sent.Add((bytes, address, port));

        public bool TryReceive(out byte[] bytes, out string address, out int port)
        {
            if (_inbox.Count == 0)
            {
                bytes = System.Array.Empty<byte>();
                address = string.Empty;
                port = 0;
                return false;
            }

            (bytes, address, port) = _inbox.Dequeue();
            return true;
        }

        public void Dispose() => Disposed = true;
    }
}
