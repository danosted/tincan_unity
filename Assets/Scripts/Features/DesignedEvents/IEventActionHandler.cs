#nullable enable
using System;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>Runs one <see cref="IEventAction"/> type on the server. Register it from the owning feature's installer.</summary>
    public interface IEventActionHandler
    {
        Type ActionType { get; }

        /// <summary>False when the action could not be applied (logged by the director, the event carries on).</summary>
        bool Execute(IEventAction action);
    }
}
