#nullable enable
using TinCan.Core.Domain.Hud;
using VContainer.Unity;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Presentation, the navigator's own peer, every frame: while they sit at the chart table, a HUD line with the chart's
    /// range, the picked waypoint's height and the controls.
    /// </summary>
    public sealed class NavigatorHudPresenter : ITickable
    {
        public const string Key = "Chart";

        private readonly ChartNavigatorUseCase _navigator;
        private readonly IHudValues _hud;

        public NavigatorHudPresenter(ChartNavigatorUseCase navigator, IHudValues hud)
        {
            _navigator = navigator;
            _hud = hud;
        }

        public void Tick()
        {
            var view = _navigator.View;
            if (view == null)
            {
                _hud.Remove(Key);
                return;
            }

            _hud.Set(Key, Line(view));
        }

        public static string Line(ChartNavigatorView view)
        {
            string range = $"{view.Range / 1000f:0.0} km";
            string picked = view.Selected >= 0 && view.Selected < view.Waypoints.Count
                ? $" | waypoint {view.Selected + 1} of {view.Waypoints.Count}, {view.Waypoints[view.Selected].Position.y:0} m"
                : $" | {view.Waypoints.Count} waypoint(s)";
            return $"{range}{picked} | click: add, pick or drag | right-click: remove | Space/Shift: height | wheel: zoom | E: leave";
        }
    }
}
