#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Abilities;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Targeting;
using UnityEngine;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Application Layer, server only, after humanoid movement. The repair ability only expresses intent: while a player
    /// carries the repairing tag (GA_RepairShip, predicted from the held Primary input), this asks the targeting service
    /// which broken part they aim at (the ability's TargetingDefinition, TD_RepairScan) and applies the repair effect
    /// to it every RepairInterval, so the rate does not depend on the tick rate. When the part reaches full health,
    /// ShipBreakageUseCase releases its breach on its next reconcile. Same shape as NetCatchUseCase: the ability sets a
    /// tag window, the feature performs the world effect.
    /// </summary>
    public class ShipRepairUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly AbilitySystemUseCase _abilities;
        private readonly ITargetingService _targeting;
        private readonly ShipDamageConfig _config;
        private readonly Dictionary<Guid, (int Target, float Accumulated)> _progress = new();

        public ShipRepairUseCase(
            INetworkService network,
            IActorRegistry actors,
            ITimeService time,
            IEventPublisher events,
            AbilitySystemUseCase abilities,
            ITargetingService targeting,
            ShipDamageConfig config)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _abilities = abilities;
            _targeting = targeting;
            _config = config;
        }

        /// <summary>
        /// On a client: the broken part the local player is predicted to be repairing (same query as the server, no effect
        /// applied). For feedback such as highlighting; the server's own query decides what is actually repaired.
        /// </summary>
        public IShipDamagePoint? PredictedTarget { get; private set; }

        public void Tick()
        {
            var targeting = _config.RepairAbility?.Targeting;
            if (_config.RepairingTag == null || _config.RepairEffect == null || targeting == null) return;

            if (!_network.IsServer)
            {
                PredictLocalTarget(targeting);
                return;
            }

            foreach (var player in _actors.GetActors<IHumanoidCharacterView>())
            {
                if (!player.HasTag(_config.RepairingTag))
                {
                    _progress.Remove(player.Id);
                    continue;
                }

                if (!_targeting.TryAcquire(new HumanoidTargeter(player), targeting, out var result) || result.Target is not IShipDamagePoint point)
                {
                    _progress.Remove(player.Id);
                    continue;
                }

                Advance(player, point);
            }
        }

        // The owner predicts: its repairing tag is predicted from the held input, so it can run the same query the server
        // will, on the same input. Only the local player; proxies are the server's business.
        private void PredictLocalTarget(TargetingDefinition targeting)
        {
            var local = _actors.GetLocalPlayerActor<IHumanoidCharacterView>();
            PredictedTarget = local != null && local.HasTag(_config.RepairingTag!) &&
                              _targeting.TryAcquire(new HumanoidTargeter(local), targeting, out var result)
                ? result.Target as IShipDamagePoint
                : null;
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
