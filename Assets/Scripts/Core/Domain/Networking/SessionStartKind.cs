#nullable enable
namespace TinCan.Core.Domain.Networking
{
    public enum SessionStartKind
    {
        Started,
        /// <summary>The host turned this client away, with a reason (another version, a full game).</summary>
        Refused,
        /// <summary>The session never came up: the host could not listen, or the address could not be reached.</summary>
        Failed,
        /// <summary>A session is already running; nothing was started.</summary>
        AlreadyActive
    }
}
