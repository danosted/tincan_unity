#nullable enable
namespace TinCan.Core.UI
{
    public enum SessionRequestKind
    {
        None,
        Host,
        Join,
        /// <summary>A dedicated server: no local player; the address and port are the listen endpoint.</summary>
        Server
    }
}
