#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;

namespace TinCan.Core.Infrastructure
{
    public class ActorRegistry : IActorRegistry
    {
        public event System.Action<IActor>? OnActorUnregistered;
        private readonly IActor? _localPlayerActor;
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

        public TActor? GetLocalPlayerActor<TActor>() where TActor : IActor
        {
            if (_localPlayerActor is TActor typedActor)
            {
                return typedActor;
            }
            // Find the humanoid that belongs to the local network client
            foreach (var actor in AllActors)
            {
                if (actor.IsSimulating && actor is TActor actorType && actor.Id != System.Guid.Empty)
                {
                    if (actor is Unity.Netcode.NetworkBehaviour netBehaviour && netBehaviour.IsOwner)
                    {
                        // Check if it's actually the humanoid character
                        if (netBehaviour.GetComponent<Features.HumanoidMovement.HumanoidControllerView>() != null)
                        {
                            return actorType;
                        }
                    }
                }
            }
            return default;
        }
    }
}
