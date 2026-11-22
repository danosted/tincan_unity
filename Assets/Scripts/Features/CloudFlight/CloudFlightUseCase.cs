#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Ship;
using TinCan.Features.CloudBoundary;

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
        private readonly Dictionary<Guid, (IAirshipView Ship, CloudFlightReading Reading)> _readings = new();

        public CloudFlightUseCase(ICloudSurfaceQuery surface, CloudFlightProcessor processor, CloudFlightConfig config)
        {
            _surface = surface;
            _processor = processor;
            _config = config;
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
            float height = HeightAboveRest(position.x, position.y, position.z);
            float fall = _readings.TryGetValue(ship.Id, out var last) ? last.Reading.Fall : 0f;
            var reading = _processor.Read(transform.forward.y, height, engineSpeed, momentum, fall, deltaTime, _config);
            _readings[ship.Id] = (ship, reading);
            return reading.Force;
        }

        /// <summary>How far a ship's origin at (x, y, z) is above where it rests on the cloud (below zero: in the cloud).</summary>
        public float HeightAboveRest(float x, float y, float z) => y - _surface.GetSurfaceHeight(x, z) - _config.RestHeight;
    }
}
