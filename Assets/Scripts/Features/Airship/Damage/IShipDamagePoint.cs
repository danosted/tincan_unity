#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Abilities;
using UnityEngine;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// A place on the ship that can break. It is a GAS actor: its health is <c>Attr_Health</c> / <c>Attr_MaxHealth</c> on
    /// its own <see cref="Controller"/>, and it changes only through gameplay effects (break, restore, the repair
    /// tool), so any damage or repair source can target it. A point stays broken until it is fully repaired, so partial
    /// repairs show progress without stopping the leak.
    /// </summary>
    public interface IShipDamagePoint
    {
        /// <summary>Stable within the fixture (sibling order), so scenarios and logs can name a point.</summary>
        int Index { get; }

        /// <summary>Health as a fraction of max; 1 until the point's attributes exist.</summary>
        float Health01 { get; }

        bool IsBroken => Health01 < 1f;
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
