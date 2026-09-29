#nullable enable
namespace TinCan.Core.Gas.Cues
{
    /// <summary>
    /// Decides where a burst cue goes, so each peer plays it exactly once:
    /// <list type="bullet">
    /// <item>The server plays it if it is also a client (a host), and sends it to the clients. If the owner predicted the
    /// same cue, the owner is left out.</item>
    /// <item>The owning client plays a predicted cue at once.</item>
    /// <item>Any other client-side apply is dropped: the server's send brings it.</item>
    /// </list>
    /// </summary>
    public sealed class GameplayCueDispatchProcessor
    {
        public GameplayCueDispatch Decide(bool isServer, bool isClient, bool isOwner, GameplayEffectContext context)
        {
            if (isServer) return new GameplayCueDispatch(playLocally: isClient, sendToClients: true, excludeOwner: context.IsPredicted);
            if (context.IsPredicted && isOwner) return new GameplayCueDispatch(playLocally: true, sendToClients: false, excludeOwner: false);
            return GameplayCueDispatch.Drop;
        }
    }
}
