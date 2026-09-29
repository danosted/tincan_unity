#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Abilities;
using UnityEngine;
using TinCan.Core.Ship;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// A place on the ship that can break. It is a GAS actor: its health is <c>Attr_Health</c> / <c>Attr_MaxHealth</c> on
    /// its own <see cref="Controller"/> (a registered <see cref="TinCan.Core.Gas.HealthAttributeSet"/>), and it changes
    /// only through gameplay effects (break, restore, the repair tool), so any damage or repair source can target it.
    /// A point is broken while its controller is damaged (<see cref="TinCan.Core.Gas.HealthQueries.IsDamaged"/>): it
    /// stays broken until fully repaired, so partial repairs show progress without stopping the leak.
    /// </summary>
    public interface IShipDamagePoint
    {
        /// <summary>Stable within the fixture (sibling order), so scenarios and logs can name a point.</summary>
        int Index { get; }

        Transform? Transform { get; }

        /// <summary>The point's ability controller: the target for break, restore and repair effects.</summary>
        IAbilityControllerBase? Controller { get; }
    }

    /// <summary>Finds the damage points under an airship (the damage-sockets fixture), in index order.</summary>
    public static class ShipDamageLocator
    {
        public static IReadOnlyList<IShipDamagePoint> FindPoints(IAirshipView airship)
        {
            // Same rule as FuelTankLocator: prefer the component's own transform, which outlives the view in teardown.
            var root = airship is Component component ? (component != null ? component.transform : null) : airship.Transform;
            return root == null
                ? System.Array.Empty<IShipDamagePoint>()
                : root.GetComponentsInChildren<IShipDamagePoint>().OrderBy(point => point.Index).ToArray();
        }
    }
}
