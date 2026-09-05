#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Application Layer: burns fuel while an airship is being driven and toggles the engine-stall ability
    /// when the tank runs dry. Ticked from the network tick right after airship movement; server only.
    /// </summary>
    public class FuelConsumptionUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "Fuel";

        private readonly INetworkService _networkService;
        private readonly IActorRegistry _actorRegistry;
        private readonly ITimeService _timeService;
        private readonly IEventPublisher _eventPublisher;
        private readonly FuelConsumptionProcessor _processor;
        private readonly Dictionary<Guid, IFuelTank> _tanks = new();
        private readonly HashSet<Guid> _stallAbilityGranted = new();

        public FuelConsumptionUseCase(
            INetworkService networkService,
            IActorRegistry actorRegistry,
            ITimeService timeService,
            IEventPublisher eventPublisher,
            FuelConsumptionProcessor processor)
        {
            _networkService = networkService;
            _actorRegistry = actorRegistry;
            _timeService = timeService;
            _eventPublisher = eventPublisher;
            _processor = processor;
        }

        public void Tick()
        {
            if (!_networkService.IsServer) return;

            foreach (var airship in _actorRegistry.GetActors<IAirshipView>())
            {
                if (!airship.IsSimulating) continue;

                var tank = ResolveTank(airship);
                if (tank?.Config == null) continue;

                var controller = (airship as IShipState)?.Controller;
                Burn(airship, tank, tank.Config, controller);
                UpdateStall(airship, tank, tank.Config, controller);
            }
        }

        private void Burn(IAirshipView airship, IFuelTank tank, FuelConfig config, IAbilityControllerBase? controller)
        {
            float throttle = airship.InputState.Throttle;
            if (!_processor.IsDriven(airship.PossessorId.HasValue, throttle)) return;

            bool boosting = controller != null && config.BoostActiveTag != null && controller.HasTag(config.BoostActiveTag);
            float drain = _processor.ComputeDrain(throttle, boosting, config.DrainPerSecondAtFullThrottle, config.BoostMultiplier, _timeService.DeltaTime);
            if (drain <= 0f) return;

            tank.Consume(drain);
        }

        private void UpdateStall(IAirshipView airship, IFuelTank tank, FuelConfig config, IAbilityControllerBase? controller)
        {
            if (controller == null || config.StallAbility == null || config.StalledTag == null) return;

            bool stalled = controller.HasTag(config.StalledTag);

            // One arm per (shouldStall, isStalled) combination; the ability is toggleable so activating flips it.
            switch (shouldStall: config.StallWhenEmpty && tank.IsEmpty, isStalled: stalled)
            {
                case (shouldStall: true, isStalled: false):
                    EnsureStallAbilityGranted(airship, controller, config);
                    controller.TryActivateAbility(config.StallAbility);
                    _eventPublisher.Publish(new FuelEmptyEvent(airship.Id));
                    _eventPublisher.LogInfo(LogSource, "Tank empty, engine stalled.");
                    break;
                case (shouldStall: false, isStalled: true):
                    controller.TryActivateAbility(config.StallAbility);
                    _eventPublisher.Publish(new FuelRestoredEvent(airship.Id));
                    _eventPublisher.LogInfo(LogSource, "Fuel restored, engine running.");
                    break;
            }
        }

        private void EnsureStallAbilityGranted(IAirshipView airship, IAbilityControllerBase controller, FuelConfig config)
        {
            if (!_stallAbilityGranted.Add(airship.Id)) return;
            controller.GrantAbility(config.StallAbility!);
        }

        private IFuelTank? ResolveTank(IAirshipView airship)
        {
            if (_tanks.TryGetValue(airship.Id, out var cached) && FuelTankLocator.IsAlive(cached)) return cached;

            var tank = FuelTankLocator.Find(airship);
            if (tank == null)
            {
                _tanks.Remove(airship.Id);
                return null;
            }

            _tanks[airship.Id] = tank;
            return tank;
        }
    }
}
