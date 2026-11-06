#nullable enable
using TinCan.Network.Infrastructure.Session;

namespace TinCan.Network.Infrastructure.Tests
{
    /// <summary>Hands out <see cref="FakeDatagramSocket"/>s and keeps the last listener and broadcaster for the test.</summary>
    public sealed class FakeDatagramSockets : IDatagramSockets
    {
        public bool ListenFails { get; set; }
        public FakeDatagramSocket? Listener { get; private set; }
        public FakeDatagramSocket? Broadcasting { get; private set; }
        public int ListenCalls { get; private set; }

        public IDatagramSocket? Listen(int port)
        {
            ListenCalls++;
            if (ListenFails) return null;
            Listener = new FakeDatagramSocket(port);
            return Listener;
        }

        public IDatagramSocket? Broadcaster()
        {
            Broadcasting = new FakeDatagramSocket(50000);
            return Broadcasting;
        }
    }
}
