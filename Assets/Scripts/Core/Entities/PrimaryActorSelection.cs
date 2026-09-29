#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;

namespace TinCan.Core.Entities
{
    /// <summary>
    /// Several components on an entity's root can be actors with the entity's id (the player: HumanoidPlayer and its
    /// ability mediator; the ship: its mediator, controller view and ability mediator). The actor registry holds one:
    /// the <see cref="ISimulatedActor"/> (the richest view, ticked by its simulation), otherwise the first.
    /// </summary>
    public static class PrimaryActorSelection
    {
        public static IActor? Choose(IEnumerable<IActor> actors)
        {
            IActor? first = null;
            foreach (var actor in actors)
            {
                if (actor is ISimulatedActor) return actor;
                first ??= actor;
            }
            return first;
        }
    }
}
