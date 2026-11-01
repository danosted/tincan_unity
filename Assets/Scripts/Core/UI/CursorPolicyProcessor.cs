#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>
    /// The cursor rule: a menu open frees it; otherwise the newest feature ask (the shipyard while looking, the free
    /// camera's toggle); otherwise it is locked while the player controls something in a session, and free when not.
    /// </summary>
    public static class CursorPolicyProcessor
    {
        public static CursorState Decide(bool menuOpen, CursorState? newestAsk, bool sessionActive, bool controlling) =>
            (menuOpen, newestAsk) switch
            {
                (true, _) => CursorState.Free,
                (false, { } ask) => ask,
                _ => sessionActive && controlling ? CursorState.Locked : CursorState.Free
            };
    }
}
