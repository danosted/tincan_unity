using System.Collections.Generic;

namespace TinCan.Core.Domain
{
    public interface IActorRegistry
    {
        event System.Action<IActor> OnActorUnregistered;
        IEnumerable<IActor> AllActors { get; }
        IEnumerable<T> GetActors<T>() where T : IActor;

        void Register(IActor actor);
        void Unregister(IActor actor);
    }
}
