#nullable enable
using System.Linq;
using System.Reflection;
using TinCan.Core.Domain.Events;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Copies every domain event published on this peer into the scenario timeline while a scenario runs, so a report
    /// shows what the game did between steps without features knowing about scenarios. Info logs are skipped (noise);
    /// warnings and errors are kept because they usually explain a failure.
    /// </summary>
    public sealed class ScenarioEventRecorder : IEventObserver
    {
        private readonly ScenarioTimeline _timeline;

        public ScenarioEventRecorder(ScenarioTimeline timeline) => _timeline = timeline;

        public void OnEvent<TEvent>(TEvent evt)
        {
            if (!_timeline.IsStarted || evt == null) return;

            switch (evt)
            {
                case LogEvent { Level: LogLevel.Info }:
                    return;
                case LogEvent log:
                    _timeline.Add("log", $"{log.Level} [{log.Source}]", log.Level != LogLevel.Error, log.Message);
                    return;
                default:
                    _timeline.Add("event", typeof(TEvent).Name, true, Describe(evt));
                    return;
            }
        }

        /// <summary>Public fields and properties as <c>Name=value</c>; events are small readonly structs without ToString.</summary>
        public static string Describe(object evt)
        {
            var type = evt.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(field => $"{field.Name}={field.GetValue(evt)}");
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Select(property => $"{property.Name}={property.GetValue(evt)}");
            return string.Join(", ", fields.Concat(properties));
        }
    }
}
