#nullable enable
using System;

namespace TinCan.Features.Navigation
{
    /// <summary>Server: a ship came close enough to a hidden point of interest, which is now on its crew's chart.</summary>
    public readonly struct PointOfInterestDiscoveredEvent
    {
        public readonly Guid RouteId;
        public readonly int PoiId;
        public readonly string Name;

        public PointOfInterestDiscoveredEvent(Guid routeId, int poiId, string name)
        {
            RouteId = routeId;
            PoiId = poiId;
            Name = name;
        }

        public override string ToString() => $"PointOfInterestDiscovered({Name} #{PoiId})";
    }
}
