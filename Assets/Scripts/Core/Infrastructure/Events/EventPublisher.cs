#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Events;

namespace TinCan.Core.Infrastructure.Events
{
    public class EventPublisher : IEventPublisher
    {
        private readonly IReadOnlyList<IEventObserver> _observers;

        public EventPublisher(IEnumerable<IEventObserver> observers)
        {
            _observers = new List<IEventObserver>(observers);
        }

        public void Publish<TEvent>(TEvent evt)
        {
            foreach (var observer in _observers)
            {
                observer.OnEvent(evt);
            }
        }
    }
}
