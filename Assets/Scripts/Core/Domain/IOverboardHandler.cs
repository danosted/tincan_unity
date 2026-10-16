#nullable enable
namespace TinCan.Core.Domain
{
    /// <summary>
    /// Decides what happens to a player who fell overboard (sank too far below the clouds), in place of the cloud
    /// boundary's teleport home. Server only. It is asked every tick the player stays below, so it must be idempotent:
    /// true while it has the player in hand. With none registered, or when every handler returns false, the cloud boundary
    /// teleports the player back aboard as before. The Respawn feature implements it (.docs/plans/overboard-and-rescue.md);
    /// register with <c>.As&lt;IOverboardHandler&gt;()</c>.
    /// </summary>
    public interface IOverboardHandler
    {
        bool TryHandleOverboard(IActor character);
    }
}
