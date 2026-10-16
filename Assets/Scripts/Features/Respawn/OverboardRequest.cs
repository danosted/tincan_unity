#nullable enable
using System.Linq;
using TinCan.Core.Domain;

namespace TinCan.Features.Respawn
{
    /// <summary>Sends this peer's choice through the RespawnState fixture and remembers it, for both menu commands.</summary>
    public static class OverboardRequest
    {
        public static void Send(IActorRegistry actors, OverboardChoice choice, OverboardChoiceKind kind, float now)
        {
            var requests = actors.GetActors<IRespawnRequests>().FirstOrDefault(candidate => candidate.IsSimulating);
            if (requests == null) return;

            requests.Request(kind);
            choice.Choose(kind, now);
        }
    }
}
