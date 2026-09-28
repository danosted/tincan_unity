#nullable enable
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>Runs <see cref="BreakShipPart"/> through <see cref="IShipBreakage"/> (server only, like every event action).</summary>
    public sealed class BreakShipPartActionHandler : EventActionHandler<BreakShipPart>
    {
        private readonly IShipBreakage _breakage;

        public BreakShipPartActionHandler(IShipBreakage breakage) => _breakage = breakage;

        protected override bool Execute(BreakShipPart action) => _breakage.TryBreak(action.Index);
    }
}
