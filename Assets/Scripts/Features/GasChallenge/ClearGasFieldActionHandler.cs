#nullable enable
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.GasChallenge
{
    /// <summary>Runs <see cref="ClearGasField"/> through <see cref="IGasField"/>.</summary>
    public sealed class ClearGasFieldActionHandler : EventActionHandler<ClearGasField>
    {
        private readonly IGasField _field;

        public ClearGasFieldActionHandler(IGasField field) => _field = field;

        protected override bool Execute(ClearGasField action)
        {
            _field.Clear();
            return true;
        }
    }
}
