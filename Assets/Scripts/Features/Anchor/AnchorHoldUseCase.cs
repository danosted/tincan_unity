#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Ship;

namespace TinCan.Features.Anchor
{
    /// <summary>
    /// Server: a ship holds still while an anchor on it is down (<see cref="IAirshipHold"/>, read by the core's
    /// movement). It holds anywhere for now; whether it should need island rock in reach is open
    /// (.docs/plans/peaceful-mode.md, P1).
    /// </summary>
    public sealed class AnchorHoldUseCase : IAirshipHold
    {
        private readonly IActorRegistry _actors;

        public AnchorHoldUseCase(IActorRegistry actors) => _actors = actors;

        public bool Holds(IAirshipView ship)
        {
            var transform = ship.Transform;
            if (transform == null) return false;

            foreach (var anchor in _actors.GetActors<IAnchor>())
            {
                if (anchor.IsSimulating && anchor.IsDown && anchor.Ship == transform) return true;
            }
            return false;
        }
    }
}
