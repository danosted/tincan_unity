#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using VContainer.Unity;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Presentation, every peer, every frame: the ship's health as a HUD meter for the whole crew. Health is the ship
    /// controller's replicated <see cref="HealthAttributeSet"/>, so hosts, clients and late joiners show the same bar.
    /// The meter goes away while there is no ship. Plan: .docs/plans/first-voyage.md (V2).
    /// </summary>
    public class ShipHealthHudPresenter : ITickable
    {
        public const string MeterKey = "Hull";

        private readonly IActorRegistry _actors;
        private readonly IHudValues _hud;

        public ShipHealthHudPresenter(IActorRegistry actors, IHudValues hud)
        {
            _actors = actors;
            _hud = hud;
        }

        public void Tick()
        {
            var controller = _actors.GetActors<IAirshipView>().OfType<IShipState>().FirstOrDefault()?.Controller;
            if (controller.TryGetHealth(out var health) && health.MaxHealth > 0f) _hud.SetMeter(MeterKey, health.HealthPercentage);
            else _hud.RemoveMeter(MeterKey);
        }
    }
}
