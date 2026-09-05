#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Abilities;
using TinCan.Features.Airship;
using Unity.Netcode;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Server-authoritative hazard loop: detonates a gas pocket the first time an airship overlaps it.
    /// </summary>
    public class GasChallengeUseCase : ITickable
    {
        private readonly IActorRegistry _actorRegistry;
        private readonly INetworkService _networkService;
        private readonly AbilitySystemUseCase _abilitySystem;

        public GasChallengeUseCase(
            IActorRegistry actorRegistry,
            INetworkService networkService,
            AbilitySystemUseCase abilitySystem)
        {
            _actorRegistry = actorRegistry;
            _networkService = networkService;
            _abilitySystem = abilitySystem;
        }

        public void Tick()
        {
            if (!_networkService.IsServer)
            {
                return;
            }

            var gasPockets = Object.FindObjectsByType<GasPocketVolume>(FindObjectsInactive.Exclude);
            if (gasPockets.Length == 0)
            {
                return;
            }

            foreach (IAirshipView airship in _actorRegistry.GetActors<IAirshipView>().Where(a => a.IsSimulating))
            {
                var shipCollider = (airship as Component)?.GetComponentInChildren<Collider>();
                if (shipCollider == null)
                {
                    continue;
                }

                if (airship is not IShipState shipState || shipState.Controller == null)
                {
                    continue;
                }

                foreach (GasPocketVolume pocket in gasPockets)
                {
                    if (!GasPocketDetonationProcessor.ShouldDetonate(pocket, shipCollider))
                    {
                        continue;
                    }

                    Detonate(pocket, shipState.Controller);
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

            if (pocket.TryGetComponent<NetworkObject>(out var networkObject) && networkObject.IsSpawned)
            {
                networkObject.Despawn(true);
            }
        }
    }
}
