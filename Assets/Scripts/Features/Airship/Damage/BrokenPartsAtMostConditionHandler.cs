#nullable enable
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>Evaluates <see cref="BrokenPartsAtMost"/> against <see cref="IShipBreakage.BrokenCount"/>.</summary>
    public sealed class BrokenPartsAtMostConditionHandler : EventConditionHandler<BrokenPartsAtMost>
    {
        private readonly IShipBreakage _breakage;

        public BrokenPartsAtMostConditionHandler(IShipBreakage breakage) => _breakage = breakage;

        protected override bool IsMet(BrokenPartsAtMost condition) => _breakage.BrokenCount <= condition.Count;
    }
}
