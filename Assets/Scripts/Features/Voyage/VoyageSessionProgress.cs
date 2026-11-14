#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Ship;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// Every peer: how far along the voyage is, as the session's progress (<see cref="ISessionProgress"/>), from the
    /// replicated state and this peer's view of the ship, the way the HUD meter reads it. 0 while briefing, 1 once
    /// arrived; under way it only moves forward within a voyage (turning away does not wind the day back), and a lost
    /// voyage holds where it was. None while idle. Plan: .docs/plans/day-cycle.md (D3).
    /// </summary>
    public sealed class VoyageSessionProgress : ISessionProgress
    {
        private readonly IActorRegistry _actors;
        private readonly VoyageRouteProcessor _route;
        private readonly VoyageConfig _config;
        private int _voyage = -1;
        private float _furthest;

        public VoyageSessionProgress(IActorRegistry actors, VoyageRouteProcessor route, VoyageConfig config)
        {
            _actors = actors;
            _route = route;
            _config = config;
        }

        public bool TryGetProgress(out float progress)
        {
            progress = 0f;
            var state = _actors.GetActors<IVoyageState>().FirstOrDefault();
            if (state == null || state.Phase == VoyagePhase.Idle) return false;

            if (state.Voyage != _voyage)
            {
                _voyage = state.Voyage;
                _furthest = 0f;
            }

            switch (state.Phase)
            {
                case VoyagePhase.Briefing:
                    _furthest = 0f;
                    break;
                case VoyagePhase.Arrived:
                    _furthest = 1f;
                    break;
                case VoyagePhase.Underway:
                    var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.Transform != null);
                    if (ship != null)
                    {
                        float now = _route.Progress(ship.Transform.position, state.Destination, _config.RouteLength);
                        if (now > _furthest) _furthest = now;
                    }

                    break;
            }

            progress = _furthest;
            return true;
        }
    }
}
