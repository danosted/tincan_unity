#nullable enable
using System;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using UnityEngine;
using VContainer;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// Server: keeps <see cref="CloudSeaConfig.AmbientHoles"/> holes open near the ships, one every
    /// <see cref="CloudSeaConfig.AmbientInterval"/> seconds, each a random size, drift and life, never under a ship. A share of
    /// them (<see cref="CloudSeaConfig.RiftChance"/>) open as rifts, long and narrow, lying at a random angle. A
    /// stand-in until the weather opens holes (plan .docs/plans/cloud-sea.md, phase 5).
    /// </summary>
    public class AmbientCloudHolesUseCase : ISimulationTickable
    {
        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly ICloudHoles _holes;
        private readonly CloudHoleProcessor _processor;
        private readonly CloudSeaConfig _config;
        private readonly System.Random _random;
        private float _nextAt = float.NegativeInfinity;

        [Inject]
        public AmbientCloudHolesUseCase(INetworkService network, IActorRegistry actors, ITimeService time,
            ICloudHoles holes, CloudHoleProcessor processor, CloudSeaConfig config)
            : this(network, actors, time, holes, processor, config, new System.Random())
        {
        }

        public AmbientCloudHolesUseCase(INetworkService network, IActorRegistry actors, ITimeService time,
            ICloudHoles holes, CloudHoleProcessor processor, CloudSeaConfig config, System.Random random)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _holes = holes;
            _processor = processor;
            _config = config;
            _random = random;
        }

        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        public void Tick()
        {
            float now = _time.Time;
            if (!_network.IsServer || _config.AmbientHoles <= 0 || now < _nextAt) return;

            int open = _holes.Holes.Count(hole => !_processor.HasClosed(hole, now));
            if (open >= _config.AmbientHoles) return;

            IAirshipView? ship = _actors.GetActors<IAirshipView>().FirstOrDefault(airship => airship.IsSimulating && airship.Transform != null);
            if (ship == null) return;

            Vector3 at = ship.Transform.position;
            if (_holes.TryOpen(Next(new Vector2(at.x, at.z), now))) _nextAt = now + _config.AmbientInterval;
        }

        private CloudHole Next(Vector2 ship, float now)
        {
            bool rift = _random.NextDouble() < _config.RiftChance;
            float radius = Between(rift ? _config.RiftRadius : _config.Radius);
            float riftAngle = (float)(_random.NextDouble() * Math.PI * 2.0);
            Vector2 riftReach = rift ? new Vector2(Mathf.Cos(riftAngle), Mathf.Sin(riftAngle)) * (Between(_config.RiftLength) * 0.5f) : Vector2.zero;
            float angle = (float)(_random.NextDouble() * Math.PI * 2.0);
            // The edge, rim included, stays at least the minimum distance from the ship, so none opens underneath; a
            // rift's ends too, whichever way it lies.
            float ragged = rift ? Mathf.Clamp01(Between(_config.RiftRagged)) : 0f;
            float away = Between(_config.Distance) + radius * (1f + ragged) * 1.5f + riftReach.magnitude;
            Vector2 centre = ship + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * away;
            float driftAngle = (float)(_random.NextDouble() * Math.PI * 2.0);
            Vector2 drift = new Vector2(Mathf.Cos(driftAngle), Mathf.Sin(driftAngle)) * (float)(_random.NextDouble() * _config.MaxDrift);
            float openSeconds = Between(_config.OpenSeconds);
            float closesAt = now + openSeconds + Between(_config.LifeSeconds);
            return new CloudHole(centre, radius, now, openSeconds, closesAt, Between(_config.CloseSeconds), drift, riftReach, ragged);
        }

        private float Between(Vector2 range) => Mathf.Lerp(range.x, Mathf.Max(range.x, range.y), (float)_random.NextDouble());
    }
}
