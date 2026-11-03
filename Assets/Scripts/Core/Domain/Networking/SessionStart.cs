#nullable enable
namespace TinCan.Core.Domain.Networking
{
    /// <summary>How hosting or joining went, and the line a menu shows when it did not.</summary>
    public readonly struct SessionStart
    {
        public const string AlreadyActiveMessage = "Already in a game.";

        public readonly SessionStartKind Kind;
        public readonly string Message;

        private SessionStart(SessionStartKind kind, string message)
        {
            Kind = kind;
            Message = message;
        }

        public bool IsStarted => Kind == SessionStartKind.Started;

        public static SessionStart Started => new(SessionStartKind.Started, string.Empty);
        public static SessionStart AlreadyActive => new(SessionStartKind.AlreadyActive, AlreadyActiveMessage);
        public static SessionStart Refused(string reason) => new(SessionStartKind.Refused, reason);
        public static SessionStart Failed(string message) => new(SessionStartKind.Failed, message);

        public override string ToString() => IsStarted ? "Started" : $"{Kind}: {Message}";
    }
}
