#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using VContainer.Unity;
using TinCan.Core.Domain.Hud;
using TinCan.Core.Ship;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Shows the first airship's tank on the headless HUD as the "Fuel" meter (level over capacity), next to the Hull
    /// meter. Runs on every peer (the level is a replicated attribute). The in-world gauge at the helm shows it too.
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
            if (tank == null || tank.Capacity <= 0f)
            {
                _hud.RemoveMeter(HudKey);
                return;
            }

            _hud.SetMeter(HudKey, tank.Level / tank.Capacity);
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
