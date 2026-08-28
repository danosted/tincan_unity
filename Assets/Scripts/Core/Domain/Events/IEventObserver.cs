#nullable enable
namespace TinCan.Core.Domain.Events
{
    // Notified of every published event regardless of type; used for logging/dev-console taps.
    public interface IEventObserver
    {
        void OnEvent<TEvent>(TEvent evt);
    }
}
