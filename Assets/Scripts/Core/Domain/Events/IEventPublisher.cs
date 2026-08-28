#nullable enable
namespace TinCan.Core.Domain.Events
{
    public interface IEventPublisher
    {
        void Publish<TEvent>(TEvent evt);
    }
}
