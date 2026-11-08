#nullable enable
using System;

namespace TinCan.Features.Gates
{
    /// <summary>
    /// One thing a gate does when the ship passes through, listed in a <see cref="GateDefinition"/> (picked by type in the
    /// Inspector). Server only; what players see and hear belongs in GAS cues. One instance serves every gate of its kind,
    /// so an action keeps no state.
    /// </summary>
    [Serializable]
    public abstract class GateAction
    {
        public abstract void Run(in GateActionContext context);
    }
}
