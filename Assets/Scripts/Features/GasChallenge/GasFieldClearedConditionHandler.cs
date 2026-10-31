#nullable enable
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.GasChallenge
{
    /// <summary>Answers <see cref="GasFieldCleared"/> from <see cref="IGasField.IsCleared"/>.</summary>
    public sealed class GasFieldClearedConditionHandler : EventConditionHandler<GasFieldCleared>
    {
        private readonly IGasField _field;

        public GasFieldClearedConditionHandler(IGasField field) => _field = field;

        protected override bool IsMet(GasFieldCleared condition) => _field.IsCleared;
    }
}
