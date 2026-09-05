#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Features.UI;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Pushes the first airship's fuel level into the headless HUD as "Fuel". Runs on every peer (the level is a
    /// replicated attribute). Replaced by the in-world gauge in slice 5.
    /// </summary>
    public class FuelHudPresenter : ITickable
    {
        public const string HudKey = "Fuel";
        private const float RelookupInterval = 1f;

        private readonly IActorRegistry _actorRegistry;
        private readonly IHudValues _hud;
        private readonly ITimeService _timeService;
        private IFuelTank? _tank;
        private float _nextLookupTime;

        public FuelHudPresenter(IActorRegistry actorRegistry, IHudValues hud, ITimeService timeService)
        {
            _actorRegistry = actorRegistry;
            _hud = hud;
            _timeService = timeService;
        }

        public void Tick()
        {
            var tank = ResolveTank();
            if (tank == null)
            {
                _hud.Remove(HudKey);
                return;
            }

            _hud.Set(HudKey, Mathf.RoundToInt(tank.Level).ToString());
        }

        private IFuelTank? ResolveTank()
        {
            if (FuelTankLocator.IsAlive(_tank)) return _tank;

            _tank = null;
            if (_timeService.Time < _nextLookupTime) return null;
            _nextLookupTime = _timeService.Time + RelookupInterval;

            var airship = _actorRegistry.GetActors<IAirshipView>().FirstOrDefault();
            if (airship == null) return null;

            _tank = FuelTankLocator.Find(airship);
            return _tank;
        }
    }
}
