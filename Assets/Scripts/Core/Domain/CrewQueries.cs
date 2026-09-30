#nullable enable
using System.Linq;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Who is aboard: the player characters in the world (<see cref="IHumanoidActor.IsPlayerCharacter"/>). One rule for
    /// every feature that sizes or gates itself by the crew, on a listen host and a dedicated server alike.
    /// </summary>
    public static class CrewQueries
    {
        public static int CrewCount(this IActorRegistry actors) =>
            actors.GetActors<IHumanoidActor>().Count(actor => actor.IsPlayerCharacter);
    }
}
