#nullable enable
using System;

namespace TinCan.DevTools
{
    /// <summary>Shared run state between the bot and telemetry, so the report is written when the route ends.</summary>
    public sealed class HarnessSession
    {
        public string? RouteName { get; private set; }
        public string CurrentStep { get; private set; } = string.Empty;
        public bool IsRouteRunning { get; private set; }
        public bool IsRouteComplete { get; private set; }

        public event Action? RouteCompleted;

        public void StartRoute(string routeName)
        {
            RouteName = routeName;
            IsRouteRunning = true;
            IsRouteComplete = false;
        }

        public void EnterStep(string label) => CurrentStep = label;

        public void CompleteRoute()
        {
            if (!IsRouteRunning) return;

            IsRouteRunning = false;
            IsRouteComplete = true;
            CurrentStep = "done";
            RouteCompleted?.Invoke();
        }
    }
}
