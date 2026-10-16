#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Ship;
using TinCan.Features.Helm;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Presentation, every peer, every frame: a compass at every helm, pointing at its ship's active waypoint. Built
    /// locally from the replicated route (<see cref="IRouteState"/>), so it needs nothing from the server; the compass is
    /// a plain prefab placed under the helm. When the route moves on to the next waypoint the arrow pulses as it swings.
    /// </summary>
    public sealed class CompassPresenter : ITickable, IDisposable
    {
        private readonly IActorRegistry _actors;
        private readonly NavigationConfig _config;
        private readonly Dictionary<IHelm, (CompassView View, int Active, int Count)> _compasses = new();
        private readonly List<IHelm> _gone = new();
        private bool _warned;

        public CompassPresenter(IActorRegistry actors, NavigationConfig config)
        {
            _actors = actors;
            _config = config;
        }

        /// <summary>The compass shown at this helm, if any (scenarios read its arrow).</summary>
        public CompassView? CompassAt(IHelm helm) => _compasses.TryGetValue(helm, out var entry) ? entry.View : null;

        public void Tick()
        {
            RemoveGone();
            foreach (var helm in _actors.GetActors<IHelm>())
            {
                if (helm is not Component component || component == null) continue;
                var ship = helm.Ship;
                if (ship == null) continue;

                if (!_compasses.TryGetValue(helm, out var entry))
                {
                    var view = Create(component.transform);
                    if (view == null) return;
                    entry = (view, -1, 0);
                }

                var route = RouteOf(ship);
                int active = route?.ActiveIndex ?? -1;
                int count = route?.Count ?? 0;
                if (active >= 0 && entry.Active >= 0 && (active != entry.Active || count != entry.Count)) entry.View.Pulse();

                Vector3? target = active >= 0 ? route!.Waypoint(active).Position : null;
                entry.View.Show(target, _config.ArrowTurnSpeed, Time.deltaTime);
                _compasses[helm] = (entry.View, active, count);
            }
        }

        public void Dispose()
        {
            foreach (var entry in _compasses.Values)
            {
                if (entry.View != null) Object.Destroy(entry.View.gameObject);
            }
            _compasses.Clear();
        }

        private IRouteState? RouteOf(IAirshipView ship) =>
            _actors.GetActors<IRouteState>().FirstOrDefault(route => ReferenceEquals(route.Ship, ship));

        private CompassView? Create(Transform helm)
        {
            if (_config.CompassPrefab == null)
            {
                if (!_warned) Debug.LogWarning("[CompassPresenter] NavigationConfig has no CompassPrefab; no compass at the helm.");
                _warned = true;
                return null;
            }

            var instance = Object.Instantiate(_config.CompassPrefab, helm, false);
            instance.name = "Compass";
            instance.transform.localPosition = _config.CompassLocalPosition;
            instance.transform.localRotation = Quaternion.identity;
            return instance.GetComponent<CompassView>() ?? instance.AddComponent<CompassView>();
        }

        private void RemoveGone()
        {
            _gone.Clear();
            foreach (var pair in _compasses)
            {
                if (pair.Key is not Component component || component == null || pair.Value.View == null) _gone.Add(pair.Key);
            }
            foreach (var helm in _gone) _compasses.Remove(helm);
        }
    }
}
