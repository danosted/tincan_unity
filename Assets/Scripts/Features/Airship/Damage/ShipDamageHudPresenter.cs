#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Features.UI;
using VContainer.Unity;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Shows "Hull breaches: n" on the headless HUD while any part is broken. Runs on every peer (damage point health
    /// is replicated) and hides the line when the hull is whole.
    /// </summary>
    public class ShipDamageHudPresenter : ITickable
    {
        public const string HudKey = "Hull breaches";

        private readonly IActorRegistry _actors;
        private readonly IHudValues _hud;

        public ShipDamageHudPresenter(IActorRegistry actors, IHudValues hud)
        {
            _actors = actors;
            _hud = hud;
        }

        public void Tick()
        {
            int broken = _actors.GetActors<IAirshipView>()
                .SelectMany(ShipDamageLocator.FindPoints)
                .Count(point => point.IsBroken);

            if (broken == 0) _hud.Remove(HudKey);
            else _hud.Set(HudKey, broken.ToString());
        }
    }
}
