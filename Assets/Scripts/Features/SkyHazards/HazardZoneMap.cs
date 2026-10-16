#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>
    /// Every peer: the current voyage's hazard zones (<see cref="HazardZoneLayoutProcessor"/>), from the replicated
    /// <see cref="ISessionLayout"/>, rebuilt when a new voyage begins. None before a voyage, or with zones switched off
    /// in the config: then the field is the same everywhere.
    /// </summary>
    public class HazardZoneMap
    {
        private readonly IActorRegistry _actors;
        private readonly HazardZoneLayoutProcessor _layout;
        private readonly SkyHazardConfig _config;
        private readonly List<HazardZone> _zones = new();
        private (int Seed, Vector3 Origin, Vector3 Destination)? _key;

        public HazardZoneMap(IActorRegistry actors, HazardZoneLayoutProcessor layout, SkyHazardConfig config)
        {
            _actors = actors;
            _layout = layout;
            _config = config;
        }

        public IReadOnlyList<HazardZone> Zones
        {
            get
            {
                Refresh();
                return _zones;
            }
        }

        /// <summary>Is the point inside a zone? (False without zones.)</summary>
        public bool IsInside(Vector3 point) => _layout.IsInside(point, Zones);

        private void Refresh()
        {
            (int Seed, Vector3 Origin, Vector3 Destination)? key = null;
            if (_config.ZonesEnabled)
            {
                foreach (var session in _actors.GetActors<ISessionLayout>())
                {
                    if (session.LayoutSeed == 0) continue;
                    key = (session.LayoutSeed, session.Origin, session.Destination);
                    break;
                }
            }

            if (key == _key) return;
            _key = key;
            _zones.Clear();
            if (key is { } layout) _layout.Zones(layout.Seed, layout.Origin, layout.Destination, _config.ZoneRules, _zones);
        }
    }
}
