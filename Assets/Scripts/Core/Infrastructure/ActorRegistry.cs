#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;

namespace TinCan.Core.Infrastructure
{
    public class ActorRegistry : IActorRegistry
    {
        public event System.Action<IActor>? OnActorUnregistered;
        private readonly List<IActor> _actors = new();

        public IEnumerable<IActor> AllActors => _actors;

        public IEnumerable<T> GetActors<T>() where T : IActor
        {
            return _actors.OfType<T>();
        }

        public void Register(IActor actor)
        {
            if (!_actors.Contains(actor))
            {
                _actors.Add(actor);
            }
        }

        public void Unregister(IActor actor)
        {
            if (_actors.Remove(actor))
            {
                OnActorUnregistered?.Invoke(actor);
            }
        }
    }
}
