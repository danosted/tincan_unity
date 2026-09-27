#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Abilities;
using TinCan.Features.HumanoidMovement;
using UnityEngine;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Application Layer, server only, after humanoid movement. The repair ability only expresses intent: while a player
    /// carries the repairing tag (GA_RepairShip, predicted from the held Primary input), this finds the broken part they
    /// face within reach and applies the repair effect to it every RepairInterval, so the rate does not depend on the
    /// tick rate. When the part reaches full health, ShipBreakageUseCase releases its breach on its next reconcile.
    /// Same shape as NetCatchUseCase: the ability sets a tag window, the feature performs the world effect.
    /// </summary>
    public class ShipRepairUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly AbilitySystemUseCase _abilities;
        private readonly RepairTargetProcessor _processor;
        private readonly ShipDamageConfig _config;
        private readonly Dictionary<Guid, (int Target, float Accumulated)> _progress = new();
        private readonly List<(int Index, Vector3 Position, bool Broken)> _parts = new();

        public ShipRepairUseCase(
            INetworkService network,
            IActorRegistry actors,
            ITimeService time,
            IEventPublisher events,
            AbilitySystemUseCase abilities,
            RepairTargetProcessor processor,
            ShipDamageConfig config)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _abilities = abilities;
            _processor = processor;
            _config = config;
        }

        public void Tick()
        {
            if (!_network.IsServer || _config.RepairingTag == null || _config.RepairEffect == null) return;

            var points = _actors.GetActors<IAirshipView>().SelectMany(ShipDamageLocator.FindPoints).ToArray();
            _parts.Clear();
            foreach (var point in points)
            {
                if (point.Transform != null) _parts.Add((point.Index, point.Transform.position, point.IsBroken));
            }

            foreach (var player in _actors.GetActors<IHumanoidCharacterView>())
            {
                var body = player.Movement?.Transform;
                if (body == null || !player.HasTag(_config.RepairingTag))
                {
                    _progress.Remove(player.Id);
                    continue;
                }

                int target = _processor.FindTarget(body.position, body.forward, _parts, _config.RepairReach, _config.RepairConeDegrees);
                if (target < 0)
                {
                    _progress.Remove(player.Id);
                    continue;
                }

                Advance(player, points.First(point => point.Index == target));
            }
        }

        private void Advance(IHumanoidCharacterView player, IShipDamagePoint point)
        {
            // Switching parts restarts the interval, so a quick glance at another part cannot bank progress.
            var progress = _progress.TryGetValue(player.Id, out var current) && current.Target == point.Index
                ? current
                : (Target: point.Index, Accumulated: 0f);

            progress.Accumulated += _time.DeltaTime;
            while (progress.Accumulated >= _config.RepairInterval && point.IsBroken && point.Controller != null)
            {
                progress.Accumulated -= _config.RepairInterval;
                _abilities.ApplyEffect(point.Controller, _config.RepairEffect!);
                _events.Publish(new ShipPartRepairTickEvent(player.Id, point.Index, point.Health01));
            }

            _progress[player.Id] = progress;
        }
    }
}
