#nullable enable
using TinCan.Core.Domain.Events;
using UnityEngine;

namespace TinCan.Core.Interaction
{
    /// <summary>Server: Interact on an <see cref="IToggleable"/> flips it (a door, the anchor) and says so.</summary>
    public sealed class ToggleInteractionHandler : IInteractionHandler
    {
        private readonly IEventPublisher _events;

        public ToggleInteractionHandler(IEventPublisher events) => _events = events;

        public void Handle(InteractionContext context)
        {
            if (context.Target is not IToggleable toggleable) return;

            bool on = toggleable.ServerToggle();
            string name = toggleable is UnityEngine.Object unityObject && unityObject != null ? unityObject.name : toggleable.GetType().Name;
            _events.Publish(new ToggledEvent(context.Requester.Id, name, on));
        }
    }

    /// <summary>Server: a player flipped a toggleable target (on: open, down, on).</summary>
    public readonly struct ToggledEvent
    {
        public readonly System.Guid ActorId;
        public readonly string Target;
        public readonly bool On;

        public ToggledEvent(System.Guid actorId, string target, bool on)
        {
            ActorId = actorId;
            Target = target;
            On = on;
        }
    }
}
