#nullable enable
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Core.Gas.Cues
{
    /// <summary>
    /// The network side of a cue target (implemented by its ability mediator): who owns it, and sending a burst cue from
    /// the server to the clients. The target is the RPC's own object, so where the cue plays is implicit and ship-local.
    /// </summary>
    public interface IGameplayCueRelay
    {
        bool IsOwnedLocally { get; }

        /// <summary>Server only: send the cue to every client except the host, and except the owner if asked.</summary>
        void RelayCue(GameplayTag cue, bool excludeOwner);
    }
}
