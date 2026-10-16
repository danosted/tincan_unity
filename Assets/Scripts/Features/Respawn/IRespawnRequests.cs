#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// A player's choice while overboard or leashed, sent from their own peer to the server (a UI choice, so an RPC, not
    /// simulated input). Lives on the RespawnState ship fixture.
    /// </summary>
    public interface IRespawnRequests : IActor
    {
        /// <summary>This peer: send the local player's choice to the server.</summary>
        void Request(OverboardChoiceKind choice);

        /// <summary>Server: the choices received since the last call.</summary>
        IReadOnlyList<RespawnRequest> TakeRequests();
    }
}
