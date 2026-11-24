#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship;
using TinCan.Features.CloudBoundary;
using UnityEngine;
using VContainer;

namespace TinCan.Features.CloudFlight
{
    /// <summary>
    /// Server: the cloud flight's forces on every ship (<see cref="IAirshipFlightForces"/>, read by the core's
    /// <see cref="AirshipMovementUseCase"/>): where the ship is against the cloud surface (<see cref="ICloudSurfaceQuery"/>:
    /// the swell and the holes, so over a hole there is no cloud), through <see cref="CloudFlightProcessor"/>. The ship's
    /// momentum is kept by the movement; this keeps each ship's last reading (its fall carries into the next step, and the dashboard shows it).
    /// </summary>
    public sealed class CloudFlightUseCase : IAirshipFlightForces
    {
        private readonly ICloudSurfaceQuery _surface;
        private readonly CloudFlightProcessor _processor;
        private readonly CloudFlightConfig _config;
        private readonly IShipHullSurfaces? _hulls;
        private readonly Dictionary<Guid, (IAirshipView Ship, CloudFlightReading Reading)> _readings = new();

        public CloudFlightUseCase(ICloudSurfaceQuery surface, CloudFlightProcessor processor, CloudFlightConfig config)
            : this(surface, processor, config, Array.Empty<IShipHullSurfaces>())
        {
        }

        /// <param name="hulls">The designed ships' hulls when the Ship Designs feature is installed: a ship rests on its
        /// keel. A ship without one rests on its origin.</param>
        [Inject]
        public CloudFlightUseCase(ICloudSurfaceQuery surface, CloudFlightProcessor processor, CloudFlightConfig config,
            IEnumerable<IShipHullSurfaces> hulls)
        {
            _surface = surface;
            _processor = processor;
            _config = config;
            _hulls = hulls.FirstOrDefault();
        }

        /// <summary>Each ship's last step (server only; empty on a client).</summary>
        public IReadOnlyDictionary<Guid, (IAirshipView Ship, CloudFlightReading Reading)> Readings => _readings;

        public AirshipFlightForce Evaluate(IAirshipView ship, float engineSpeed, float momentum, float deltaTime)
        {
            var transform = ship.Transform;
            if (transform == null)
            {
                _readings.Remove(ship.Id);
                return default;
            }

            var position = transform.position;
            float height = HeightAboveRest(ship);
            float fall = _readings.TryGetValue(ship.Id, out var last) ? last.Reading.Fall : 0f;
            float seaTilt = SeaTilt(position, transform.forward);
            var reading = _processor.Read(transform.forward.y, height, engineSpeed, momentum, fall, deltaTime, _config, seaTilt);
            _readings[ship.Id] = (ship, reading);
            return reading.Force;
        }

        /// <summary>
        /// The sea's slope along the heading, in degrees (above zero: it rises ahead): the surface read under the ship and
        /// ahead of and behind it, so bumps shorter than the reach smooth out. A reading that drops away steeper than 45°
        /// has fallen into a hole or a rift, not down a swell, and is left out: a rift ahead does not tip the nose into it.
        /// Zero with the nose straight up or down (no heading), or with both readings in a hole.
        /// </summary>
        public float SeaTilt(Vector3 position, Vector3 forward)
        {
            var heading = new Vector3(forward.x, 0f, forward.z);
            if (heading.sqrMagnitude < 1e-4f) return 0f;
            float reach = _config.ContourReach;
            heading = heading.normalized * reach;

            float centre = _surface.GetSurfaceHeight(position.x, position.z);
            float rise = 0f;
            int parts = 0;

            float ahead = _surface.GetSurfaceHeight(position.x + heading.x, position.z + heading.z);
            if (centre - ahead <= reach) { rise += ahead - centre; parts++; }
            float behind = _surface.GetSurfaceHeight(position.x - heading.x, position.z - heading.z);
            if (centre - behind <= reach) { rise += centre - behind; parts++; }

            return parts == 0 ? 0f : Mathf.Atan2(rise / parts, reach) * Mathf.Rad2Deg;
        }

        /// <summary>How far a ship's keel is above where it rests on the cloud (below zero: in the cloud). The keel is
        /// read straight down from the origin, along the ship's up; a ship with no designed hull uses its origin.</summary>
        public float HeightAboveRest(IAirshipView ship)
        {
            var transform = ship.Transform;
            if (transform == null) return 0f;
            float keel = _hulls?.KeelOf(ship.Id) ?? 0f;
            var bottom = transform.position + transform.up * keel;
            return bottom.y - _surface.GetSurfaceHeight(bottom.x, bottom.z) - _config.RestHeight;
        }
    }
}
