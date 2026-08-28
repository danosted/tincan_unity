#nullable enable
using TinCan.Core.Domain.Events;
using UnityEngine;

namespace TinCan.Core.Infrastructure.Events
{
    public class DebugLogEventObserver : IEventObserver
    {
        public void OnEvent<TEvent>(TEvent evt)
        {
            if (evt is LogEvent logEvent)
            {
                switch (logEvent.Level)
                {
                    case LogLevel.Warning:
                        Debug.LogWarning(logEvent.ToString());
                        return;
                    case LogLevel.Error:
                        Debug.LogError(logEvent.ToString());
                        return;
                    default:
                        Debug.Log(logEvent.ToString());
                        return;
                }
            }

            Debug.Log($"[Event] {typeof(TEvent).Name}: {evt}");
        }
    }
}
