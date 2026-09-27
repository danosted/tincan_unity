#nullable enable
namespace TinCan.Core.Domain.Cues
{
    /// <summary>How this peer relates to a cue's target: the server (a host included), the target's owning client, or
    /// any other client. Lets a handler skip, say, a HUD toast for everyone but the owner.</summary>
    public enum GameplayCuePeerRole
    {
        Server,
        Owner,
        Proxy
    }
}
