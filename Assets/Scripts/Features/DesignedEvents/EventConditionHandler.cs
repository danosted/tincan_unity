#nullable enable
using System;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>Typed base for <see cref="IEventConditionHandler"/>: implement <see cref="IsMet(TCondition)"/> only.</summary>
    public abstract class EventConditionHandler<TCondition> : IEventConditionHandler where TCondition : IEventCondition
    {
        public Type ConditionType => typeof(TCondition);

        public bool IsMet(IEventCondition condition) => condition is TCondition typed && IsMet(typed);

        protected abstract bool IsMet(TCondition condition);
    }
}
