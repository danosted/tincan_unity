#nullable enable
using TinCan.Core.Gas;
using TinCan.Core.Ship;

namespace TinCan.Features.Gates
{
    /// <summary>What a <see cref="GateAction"/> works with: the ship that passed, the gate, and the services an action may use.</summary>
    public readonly struct GateActionContext
    {
        public readonly IAirshipView Ship;
        public readonly IGate Gate;
        public readonly AbilitySystemUseCase Abilities;

        public GateActionContext(IAirshipView ship, IGate gate, AbilitySystemUseCase abilities)
        {
            Ship = ship;
            Gate = gate;
            Abilities = abilities;
        }
    }
}
