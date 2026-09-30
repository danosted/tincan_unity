#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;
using TinCan.Core.Ship;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// Presentation, every peer, every frame: the voyage on the HUD. A "Voyage" meter fills with progress, and a line
    /// says what matters now: the countdown, then distance and where the destination lies (for the pilot), then the
    /// outcome. Reads the replicated state and this peer's view of the ship, so it needs nothing from the server.
    /// </summary>
    public class VoyageHudPresenter : ITickable
    {
        public const string Key = "Voyage";

        // Within this many degrees of the bow the destination counts as dead ahead.
        private const float AheadDegrees = 5f;

        private readonly IActorRegistry _actors;
        private readonly IHudValues _hud;
        private readonly VoyageRouteProcessor _route;
        private readonly VoyageConfig _config;

        public VoyageHudPresenter(IActorRegistry actors, IHudValues hud, VoyageRouteProcessor route, VoyageConfig config)
        {
            _actors = actors;
            _hud = hud;
            _route = route;
            _config = config;
        }

        public void Tick()
        {
            var state = _actors.GetActors<IVoyageState>().FirstOrDefault();
            var ship = _actors.GetActors<IAirshipView>().FirstOrDefault(candidate => candidate.Transform != null);
            if (state == null || ship == null || state.Phase == VoyagePhase.Idle)
            {
                _hud.Remove(Key);
                _hud.RemoveMeter(Key);
                return;
            }

            Vector3 position = ship.Transform.position;
            float progress = state.Phase == VoyagePhase.Arrived ? 1f : _route.Progress(position, state.Destination, _config.RouteLength);
            _hud.SetMeter(Key, state.Phase == VoyagePhase.Briefing ? 0f : progress);
            _hud.Set(Key, Line(state, position, ship.Transform.rotation));
        }

        private string Line(IVoyageState state, Vector3 position, Quaternion rotation)
        {
            switch (state.Phase)
            {
                case VoyagePhase.Briefing:
                    return $"voyage {state.Voyage}, casting off in {state.BriefingSecondsLeft} s";
                case VoyagePhase.Arrived:
                    return "arrived!";
                case VoyagePhase.Lost:
                    return "the ship is lost";
                default:
                    float km = _route.DistanceLeft(position, state.Destination) / 1000f;
                    return $"{km:0.0} km to go, {Direction(_route.Bearing(position, rotation, state.Destination))}";
            }
        }

        private static string Direction(float bearing)
        {
            if (Mathf.Abs(bearing) <= AheadDegrees) return "dead ahead";
            return $"{Mathf.Abs(bearing):0}° to {(bearing > 0f ? "starboard" : "port")}";
        }
    }
}
