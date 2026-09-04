#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Airship;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.GasChallenge
{
    public class GasChallengeUseCase : ITickable
    {
        private readonly IActorRegistry _actorRegistry;
        private readonly INetworkService _networkService;
        private readonly ITimeService _timeService;
        private readonly Dictionary<Guid, GasPocketResult> _airshipGasStates = new();

        public GasChallengeUseCase(
            IActorRegistry actorRegistry,
            INetworkService networkService,
            ITimeService timeService)
        {
            _actorRegistry = actorRegistry;
            _networkService = networkService;
            _timeService = timeService;
        }

        public bool IsAirshipInDanger(IAirshipView airship)
        {
            return _airshipGasStates.TryGetValue(airship.Id, out var state) && state.IsInGas;
        }

        public float GetDangerLevel(IAirshipView airship)
        {
            if (!_airshipGasStates.TryGetValue(airship.Id, out var state))
            {
                return 0f;
            }

            return state.WarningLevel;
        }

        public void Tick()
        {
            if (!_networkService.IsServer)
            {
                return;
            }

            var gasPockets = UnityEngine.Object.FindObjectsByType<GasPocketVolume>(FindObjectsSortMode.None);
            foreach (IAirshipView airship in _actorRegistry.GetActors<IAirshipView>().Where(a => a.IsSimulating))
            {
                var state = new GasPocketResult(false, 0f, 0f);

                var shipCollider = (airship as Component)?.GetComponentInChildren<Collider>();
                if (shipCollider == null)
                {
                    continue;
                }

                foreach (GasPocketVolume pocket in gasPockets)
                {
                    var result = GasPocketProcessor.EvaluateAirship(
                        state,
                        shipCollider,
                        pocket,
                        _timeService.DeltaTime);

                    if (result.IsInGas)
                    {
                        state = result;
                    }
                }

                _airshipGasStates[airship.Id] = state;

                if (state.DamageThisTick > 0f && airship is IHealth health)
                {
                    health.ApplyDamage(state.DamageThisTick);
                }
            }
        }
    }
}
