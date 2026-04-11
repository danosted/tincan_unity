using System;

namespace TinCan.Core.Domain.Networking
{
    /// <summary>
    /// Decoupled messaging system for networking events.
    /// Allows publishing and subscribing to network-synced events without referencing specific network libraries.
    /// </summary>
    public interface INetworkEventBus
    {
        void Publish<T>(T message) where T : struct;
        void Subscribe<T>(Action<T> handler) where T : struct;
        void Unsubscribe<T>(Action<T> handler) where T : struct;
    }
}
