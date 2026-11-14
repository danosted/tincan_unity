#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Targeting;
using TinCan.Features.SkillChecks;
using UnityEngine;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Application Layer, after humanoid movement; repairs on the server only. The repair ability only expresses intent: when a player
    /// starts carrying the repairing tag (GA_RepairShip, predicted from the held Primary input), this asks the targeting
    /// service which broken part they aim at (the ability's TargetingDefinition, TD_RepairScan) and opens that part's
    /// repair check for them (the <see cref="ISkillCheckHost"/> beside the part, SC_ShipRepair). One check per pull: the
    /// grade's effect goes on the part itself (Perfect fixes it, Good heals half, a Miss nothing), and the player pulls
    /// again until it is whole. When the part reaches full health, ShipBreakageUseCase releases its breach on its next
    /// reconcile. On the local player's own peer it also runs that query every tick while they hold the tool
    /// (<see cref="AimedTarget"/>), so what a pull would repair can be shown beforehand. Plan: hazards-and-boons.md (R1).
    /// </summary>
    public class ShipRepairUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private const string LogSource = "ShipRepair";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly IEventPublisher _events;
        private readonly AbilitySystemUseCase _abilities;
        private readonly ITargetingService _targeting;
        private readonly ISkillChecks _checks;
        private readonly ShipDamageConfig _config;
        // Players whose current pull already opened a check (or found the part busy): the next one needs a new pull.
        private readonly HashSet<Guid> _pulled = new();

        public ShipRepairUseCase(
            INetworkService network,
            IActorRegistry actors,
            IEventPublisher events,
            AbilitySystemUseCase abilities,
            ITargetingService targeting,
            ISkillChecks checks,
            ShipDamageConfig config)
        {
            _network = network;
            _actors = actors;
            _events = events;
            _abilities = abilities;
            _targeting = targeting;
            _checks = checks;
            _config = config;
        }

        /// <summary>
        /// On a client: the broken part the local player is predicted to be repairing (same query as the server, no effect
        /// applied). The server's own query decides what is actually repaired.
        /// </summary>
        public IShipDamagePoint? PredictedTarget { get; private set; }

        /// <summary>
        /// On the local player's own peer (host or client): the broken part a repair would work on right now, while they
        /// hold the repair tool, whether or not they are repairing. The same query the server runs on a pull, so the
        /// player can see what they aim at before they pull the trigger (<see cref="RepairAimHighlightPresenter"/>).
        /// </summary>
        public IShipDamagePoint? AimedTarget { get; private set; }

        public void Tick()
        {
            var targeting = _config.RepairAbility?.Targeting;
            if (_config.RepairingTag == null || targeting == null) return;

            AimLocalPlayer(targeting);
            if (!_network.IsServer) return;

            foreach (var player in _actors.GetActors<IHumanoidCharacterView>())
            {
                if (!player.HasTag(_config.RepairingTag))
                {
                    _pulled.Remove(player.Id);
                    continue;
                }

                // A pull that starts off target opens the check once the player turns to a broken part.
                if (_pulled.Contains(player.Id)) continue;
                if (!_targeting.TryAcquire(new HumanoidTargeter(player), targeting, out var result) || result.Target is not IShipDamagePoint point) continue;

                _pulled.Add(player.Id);
                Open(player, point);
            }
        }

        // The owner runs the query the server will, on the same input: while it holds the tool (the ability is granted on
        // the owner too, for prediction) to show what it aims at, and while repairing to predict the target. Only the
        // local player; proxies are the server's business.
        private void AimLocalPlayer(TargetingDefinition targeting)
        {
            var local = _actors.GetLocalPlayerActor<IHumanoidCharacterView>();
            bool repairing = local != null && local.HasTag(_config.RepairingTag!);
            bool holdsTool = local != null && (repairing || _abilities.HasAbility(local, _config.RepairAbility!));

            AimedTarget = holdsTool && _targeting.TryAcquire(new HumanoidTargeter(local!), targeting, out var result)
                ? result.Target as IShipDamagePoint
                : null;
            PredictedTarget = !_network.IsServer && repairing ? AimedTarget : null;
        }

        private void Open(IHumanoidCharacterView player, IShipDamagePoint point)
        {
            var host = point.Transform != null ? point.Transform.GetComponent<ISkillCheckHost>() : null;
            if (host == null || point.Controller == null)
            {
                _events.LogWarning(LogSource, $"Part {point.Index} has no repair check (SkillCheckHostNetworkMediator).");
                return;
            }

            if (player.OwnerId is not { } clientId) return;
            _checks.TryOpen(host, clientId, point.Controller);
        }
    }
}
