#nullable enable
using System;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>Evaluates one <see cref="IEventCondition"/> type on the server. Register it from the owning feature's installer.</summary>
    public interface IEventConditionHandler
    {
        Type ConditionType { get; }

        bool IsMet(IEventCondition condition);
    }
}
