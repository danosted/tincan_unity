#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Features.Gates.Actions
{
    /// <summary>Puts a GAS effect on the ship (a boon gate: GE_Boon_SpeedBurst). Its cue says what happened.</summary>
    [Serializable]
    public sealed class ApplyEffectGateAction : GateAction
    {
        [Tooltip("The effect the ship gets.")]
        public GameplayEffectDefinition? Effect;

        public override void Run(in GateActionContext context)
        {
            var controller = (context.Ship as IShipState)?.Controller;
            if (controller != null && Effect != null) context.Abilities.ApplyEffect(controller, Effect);
        }
    }
}
