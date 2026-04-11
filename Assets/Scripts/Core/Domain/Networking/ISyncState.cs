using System;

namespace TinCan.Core.Domain.Networking
{
    /// <summary>
    /// Abstract interface for a value that should be synchronized across the network.
    /// The actual implementation (e.g., using NGO's NetworkVariable) is handled in the Infrastructure layer.
    /// </summary>
    public interface ISyncState<T>
    {
        T Value { get; set; }
        event Action<T> OnChanged;
    }
}
