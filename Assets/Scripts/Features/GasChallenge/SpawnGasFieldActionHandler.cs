#nullable enable
using TinCan.Features.DesignedEvents;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Runs <see cref="SpawnGasField"/> through <see cref="IGasField"/> (server only, like every event action); fails when
    /// no pocket could be placed.
    /// </summary>
    public sealed class SpawnGasFieldActionHandler : EventActionHandler<SpawnGasField>
    {
        private readonly IGasField _field;

        public SpawnGasFieldActionHandler(IGasField field) => _field = field;

        protected override bool Execute(SpawnGasField action) => _field.Spawn(action.Count, action.Ahead, action.Spread) > 0;
    }
}
