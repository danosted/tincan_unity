#nullable enable
using System;

namespace TinCan.Network.Infrastructure.Session
{
    /// <summary>A UDP socket polled once a frame: no threads, no callbacks. The seam discovery is tested through.</summary>
    public interface IDatagramSocket : IDisposable
    {
        void Send(byte[] bytes, string address, int port);

        /// <summary>The next waiting datagram and its sender; false when none is waiting.</summary>
        bool TryReceive(out byte[] bytes, out string address, out int port);
    }
}
