#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Abilities;
using TinCan.Features.Airship;
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Server-authoritative hazard loop, on the simulation tick: a gas pocket detonates the first time an airship
    /// touches it, applying its explosion effect to the ship.
    /// </summary>
    public class GasChallengeUseCase : ISimulationTickable
    {
        private readonly IActorRegistry _actorRegistry;
        private readonly INetworkService _networkService;
        private readonly AbilitySystemUseCase _abilitySystem;
        private readonly IGasPocketQuery _pockets;
        private readonly List<GasPocketVolume> _touching = new();

        public GasChallengeUseCase(
            IActorRegistry actorRegistry,
            INetworkService networkService,
            AbilitySystemUseCase abilitySystem,
            IGasPocketQuery pockets)
        {
            _actorRegistry = actorRegistry;
            _networkService = networkService;
            _abilitySystem = abilitySystem;
            _pockets = pockets;
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
                    if (!pocket.HasDetonated) Detonate(pocket, controller);
                }
            }
        }

        private void Detonate(GasPocketVolume pocket, IAbilityControllerBase target)
        {
            pocket.MarkDetonated();

            if (pocket.ExplosionEffect == null)
            {
                Debug.LogWarning($"[GasChallengeUseCase] Gas pocket '{pocket.name}' has no explosion effect assigned.");
                return;
            }

            _abilitySystem.ApplyGameplayEffect(target, pocket.ExplosionEffect);
        }
    }
}
