#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;

namespace TinCan.Core.Gas
{
    /// <summary>
    /// Picks the one ability controller per actor that GAS registers and ticks. A player carries two controllers with
    /// one Id (<c>HumanoidPlayer</c> and the <c>AbilityNetworkMediator</c> it delegates to); registering both ticked
    /// its effects on two clocks. The <see cref="ISimulatedActor"/> wins, since its movement loop ticks it; otherwise
    /// the first controller found keeps the Id.
    /// </summary>
    public static class AbilityControllerSelection
    {
        public static List<IAbilityControllerBase> OnePerActor(IEnumerable<IAbilityControllerBase> controllers)
        {
            var chosen = new List<IAbilityControllerBase>();
            var indexById = new Dictionary<Guid, int>();

            foreach (var controller in controllers)
            {
                if (!indexById.TryGetValue(controller.Id, out int index))
                {
                    indexById.Add(controller.Id, chosen.Count);
                    chosen.Add(controller);
                }
                else if (controller is ISimulatedActor && chosen[index] is not ISimulatedActor)
                {
                    chosen[index] = controller;
                }
            }

            return chosen;
        }
    }
}
