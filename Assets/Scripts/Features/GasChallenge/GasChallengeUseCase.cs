#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Features.Airship.Damage;
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Server-authoritative hazard loop, on the simulation tick: a gas pocket detonates the first time an airship
    /// touches it, applying its explosion effect to the ship, and the blast breaks the damage points within reach of the
    /// pocket (its radius plus <see cref="GasChallengeConfig.BlastRadius"/>), fittings first
    /// (<see cref="GasBlastProcessor"/>). It breaks them through <see cref="IShipBreakage"/>, so the crew repairs them like
    /// any break.
    /// </summary>
    public class GasChallengeUseCase : ISimulationTickable
    {
        private const string LogSource = "GasChallenge";

        private readonly IActorRegistry _actorRegistry;
        private readonly INetworkService _networkService;
        private readonly AbilitySystemUseCase _abilitySystem;
        private readonly IGasPocketQuery _pockets;
        private readonly IShipBreakage _breakage;
        private readonly GasBlastProcessor _blast;
        private readonly GasChallengeConfig _config;
        private readonly IEventPublisher _events;
        private readonly List<GasPocketVolume> _touching = new();

        public GasChallengeUseCase(
            IActorRegistry actorRegistry,
            INetworkService networkService,
            AbilitySystemUseCase abilitySystem,
            IGasPocketQuery pockets,
            IShipBreakage breakage,
            GasBlastProcessor blast,
            GasChallengeConfig config,
            IEventPublisher events)
        {
            _actorRegistry = actorRegistry;
            _networkService = networkService;
            _abilitySystem = abilitySystem;
            _pockets = pockets;
            _breakage = breakage;
            _blast = blast;
            _config = config;
            _events = events;
        }

        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        public void Tick()
        {
            if (!_networkService.IsServer) return;

            foreach (var airship in _actorRegistry.GetActors<IAirshipView>())
            {
                if (!airship.IsSimulating || airship is not IShipState { Controller: { } controller }) continue;

                _pockets.Touching(airship, _touching);
                foreach (var pocket in _touching)
                {
                    if (!pocket.HasDetonated) Detonate(airship, pocket, controller);
                }
            }
        }

        private void Detonate(IAirshipView airship, GasPocketVolume pocket, IAbilityControllerBase target)
        {
            pocket.MarkDetonated();

            if (pocket.ExplosionEffect == null) Debug.LogWarning($"[GasChallengeUseCase] Gas pocket '{pocket.name}' has no explosion effect.");
            else _abilitySystem.ApplyGameplayEffect(target, pocket.ExplosionEffect);

            Blast(airship, pocket);
        }

        private void Blast(IAirshipView airship, GasPocketVolume pocket)
        {
            var healthy = ShipDamageLocator.FindPoints(airship)
                .Where(point => point.Transform != null && !point.Controller.IsDamaged())
                .Select(point => new BlastTarget(point.Index, point.Transform!.position, point.Socket != null));

            var broken = _blast.Pick(pocket.Center, healthy, pocket.Radius + _config.BlastRadius, _config.MaxBreaks)
                .Where(_breakage.TryBreak)
                .ToList();
            _events.LogInfo(LogSource, broken.Count == 0
                ? "Gas pocket went off; nothing in reach broke."
                : $"Gas pocket went off; broke point(s) {string.Join(", ", broken)}.");
        }
    }
}
