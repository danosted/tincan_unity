#nullable enable
using System;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>Typed base for <see cref="IEventActionHandler"/>: implement <see cref="Execute(TAction)"/> only.</summary>
    public abstract class EventActionHandler<TAction> : IEventActionHandler where TAction : IEventAction
    {
        public Type ActionType => typeof(TAction);

        public bool Execute(IEventAction action) => action is TAction typed && Execute(typed);

        protected abstract bool Execute(TAction action);
    }
}
