#nullable enable
namespace TinCan.Core.Domain.Networking
{
    /// <summary>Why a session ended, and the line the menu shows for it (empty when this player left on purpose).</summary>
    public readonly struct SessionEnd
    {
        public const string ConnectionLostMessage = "Connection lost.";

        public readonly SessionEndKind Kind;
        public readonly string Message;

        public SessionEnd(SessionEndKind kind, string message)
        {
            Kind = kind;
            Message = message;
        }

        public static SessionEnd Left => new(SessionEndKind.Left, string.Empty);

        /// <summary>A client's disconnect: refused with the server's reason when it gave one, lost otherwise.</summary>
        public static SessionEnd FromDisconnect(string? reason) =>
            string.IsNullOrWhiteSpace(reason)
                ? new SessionEnd(SessionEndKind.ConnectionLost, ConnectionLostMessage)
                : new SessionEnd(SessionEndKind.Refused, reason!);
    }
}
