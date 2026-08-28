#nullable enable

namespace TinCan.Core.Domain.Events
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    // Generic catch-all for ad hoc diagnostic messages, replacing scattered Debug.* calls.
    public readonly struct LogEvent
    {
        public readonly string Source;
        public readonly string Message;
        public readonly LogLevel Level;

        public LogEvent(string source, string message, LogLevel level = LogLevel.Info)
        {
            Source = source;
            Message = message;
            Level = level;
        }

        public override string ToString() => $"[{Source}] {Message}";
    }

    public static class EventPublisherLogExtensions
    {
        public static void LogInfo(this IEventPublisher publisher, string source, string message) =>
            publisher.Publish(new LogEvent(source, message, LogLevel.Info));

        public static void LogWarning(this IEventPublisher publisher, string source, string message) =>
            publisher.Publish(new LogEvent(source, message, LogLevel.Warning));

        public static void LogError(this IEventPublisher publisher, string source, string message) =>
            publisher.Publish(new LogEvent(source, message, LogLevel.Error));
    }
}
