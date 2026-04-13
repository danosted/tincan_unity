using System;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// The most basic unit of simulation in the world.
    /// </summary>
    public interface IActor
    {
        Guid Id { get; }
        bool IsSimulating { get; }
    }
}
